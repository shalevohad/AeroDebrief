using System;
using System.Data;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AeroDebrief.Core.Interfaces.Storage;
using AeroDebrief.Core.Storage.Abstractions;
using Dapper;
using Microsoft.Data.Sqlite;
using NLog;

namespace AeroDebrief.Core.Storage.Sqlite
{
    /// <summary>
    /// SQLite Unit of Work implementation.
    /// Manages a single persistent connection for optimal performance.
    /// Implements WAL mode for concurrent read/write access.
    /// </summary>
    public class SqliteUnitOfWork : IUnitOfWork
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        
        private readonly SqliteConnection _connection;
        private readonly string _filePath;
        private IDbTransaction? _currentTransaction;
        private bool _disposed;

        // Repositories
        private SqlitePacketRepository? _packets;
        private SqliteFrequencyRepository? _frequencies;
        private SqlitePlayerRepository? _players;
        private SqliteRecordingRepository? _recording;
        private SqliteAmplitudeRepository? _amplitudes; // ? NEW: Amplitude cache repository

        public SqliteUnitOfWork(string filePath, bool createNew = false)
        {
            _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));

            // Phase 6 Optimization: Create connection string with pooling and performance optimizations
            var builder = new SqliteConnectionStringBuilder
            {
                DataSource = filePath,
                Mode = createNew ? SqliteOpenMode.ReadWriteCreate : SqliteOpenMode.ReadWrite,
                Cache = SqliteCacheMode.Shared,
                // Phase 6: Enable connection pooling for better performance
                Pooling = true
            };

            _connection = new SqliteConnection(builder.ToString());
        }

        public IPacketRepository Packets => 
            _packets ??= new SqlitePacketRepository(_connection, _filePath);

        public IFrequencyRepository Frequencies => 
            _frequencies ??= new SqliteFrequencyRepository(_connection);

        public IPlayerRepository Players => 
            _players ??= new SqlitePlayerRepository(_connection);

        public IRecordingRepository Recording => 
            _recording ??= new SqliteRecordingRepository(_connection);

        /// <summary>
        /// ? NEW: Amplitude cache repository for instant waveform rendering.
        /// Provides pre-computed amplitude data without re-decoding audio.
        /// </summary>
        public IAmplitudeRepository Amplitudes => 
            _amplitudes ??= new SqliteAmplitudeRepository(_connection);

        /// <summary>
        /// Initialize the Unit of Work (open connection, configure WAL mode, create schema)
        /// </summary>
        public async Task InitializeAsync(RecordingMetadata? metadata = null, CancellationToken ct = default)
        {
            try
            {
                Logger.Debug("Opening SQLite connection...");
                if (_connection.State != ConnectionState.Open)
                {
                    await _connection.OpenAsync(ct).ConfigureAwait(false);
                    Logger.Debug("Connection opened successfully");
                }

                // Configure SQLite for optimal performance
                Logger.Debug("Configuring connection...");
                await ConfigureConnectionAsync(ct).ConfigureAwait(false);

                // Create schema if this is a new database
                if (metadata != null)
                {
                    Logger.Debug("Creating new database schema...");
                    await CreateSchemaAsync(ct).ConfigureAwait(false);
                    
                    Logger.Debug("Updating recording metadata...");
                    await Recording.UpdateMetadataAsync(metadata, ct).ConfigureAwait(false);
                    
                    Logger.Debug("Initializing packet repository...");
                    await Packets.InitializeAsync(metadata, ct).ConfigureAwait(false);
                }
                else
                {
                    Logger.Debug("Opening existing database...");
                    await Packets.OpenAsync(ct).ConfigureAwait(false);
                }

                Logger.Info($"? SQLite Unit of Work initialized: {_filePath}");
            }
            catch (OperationCanceledException)
            {
                Logger.Warn("SQLite initialization was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to initialize SQLite Unit of Work");
                throw;
            }
        }

        /// <summary>
        /// Configure SQLite connection for optimal performance.
        /// Phase 6 Optimization: Enhanced pragmas for better performance and caching.
        /// - Enables WAL mode (critical for concurrent access!)
        /// - Sets cache size and memory-mapped I/O
        /// - Optimizes synchronous mode and temp storage
        /// </summary>
        private async Task ConfigureConnectionAsync(CancellationToken ct = default)
        {
            Logger.Debug("Configuring SQLite for optimal performance...");

            // CRITICAL: Enable WAL mode for concurrent read/write
            await _connection.ExecuteAsync("PRAGMA journal_mode=WAL");

            // Phase 6 Performance optimizations (enhanced from Phase 4)
            await _connection.ExecuteAsync("PRAGMA synchronous=NORMAL");        // Balance safety/performance
            await _connection.ExecuteAsync("PRAGMA cache_size=-64000");         // 64MB cache (negative = KB)
            await _connection.ExecuteAsync("PRAGMA temp_store=MEMORY");         // Memory for temp tables
            await _connection.ExecuteAsync("PRAGMA mmap_size=268435456");       // 256MB memory-mapped I/O
            await _connection.ExecuteAsync("PRAGMA page_size=4096");            // Optimal page size for most systems
            
            // Phase 6: Additional optimizations
            await _connection.ExecuteAsync("PRAGMA locking_mode=NORMAL");       // Allow multiple connections (NORMAL, not EXCLUSIVE)
            await _connection.ExecuteAsync("PRAGMA read_uncommitted=1");        // Allow dirty reads for better concurrency
            await _connection.ExecuteAsync("PRAGMA wal_autocheckpoint=1000");   // Checkpoint every 1000 pages
            await _connection.ExecuteAsync("PRAGMA optimize");                  // Analyze and optimize queries

            Logger.Debug("? SQLite configured with WAL mode and performance optimizations (Phase 6 enhanced)");
        }

        /// <summary>
        /// Create database schema
        /// </summary>
        private async Task CreateSchemaAsync(CancellationToken ct = default)
        {
            Logger.Debug("Creating database schema...");

            var schemaPath = Path.Combine(
                Path.GetDirectoryName(typeof(SqliteUnitOfWork).Assembly.Location)!,
                "Storage", "Schema.sqlite.sql");

            if (!File.Exists(schemaPath))
            {
                schemaPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, 
                    "Storage", "Schema.sqlite.sql");
            }

            if (!File.Exists(schemaPath))
            {
                throw new FileNotFoundException($"Schema file not found: {schemaPath}");
            }

            Logger.Debug($"Reading schema from: {schemaPath}");
            
            try
            {
                // Check file size and accessibility
                var fileInfo = new FileInfo(schemaPath);
                Logger.Debug($"Schema file size: {fileInfo.Length} bytes");
                
                // Use synchronous read to avoid async deadlock issues
                Logger.Debug("Reading file content synchronously...");
                string schema;
                using (var reader = new StreamReader(schemaPath, System.Text.Encoding.UTF8))
                {
                    schema = reader.ReadToEnd();
                }
                Logger.Debug($"Schema read successfully, length: {schema.Length} characters");
                
                // Parse SQL statements properly, handling comments and multi-line statements
                var statements = ParseSqlStatements(schema);
                
                Logger.Debug($"Executing {statements.Count} SQL statements...");
                
                int executedCount = 0;
                foreach (var statement in statements)
                {
                    if (ct.IsCancellationRequested)
                        break;
                    
                    if (string.IsNullOrWhiteSpace(statement))
                        continue;
                        
                    try
                    {
                        Logger.Debug($"Executing statement {executedCount + 1}: {statement.Substring(0, Math.Min(50, statement.Length))}...");
                        await _connection.ExecuteAsync(statement).ConfigureAwait(false);
                        executedCount++;
                        
                        if (executedCount % 5 == 0) // Log every 5 statements
                        {
                            Logger.Debug($"Executed {executedCount}/{statements.Count} statements...");
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex, $"Failed to execute statement {executedCount + 1}: {statement.Substring(0, Math.Min(200, statement.Length))}...");
                        throw;
                    }
                }
                
                Logger.Debug($"? Database schema created successfully ({executedCount} statements executed)");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to create schema");
                throw;
            }
        }

        /// <summary>
        /// Parse SQL statements from schema file, properly handling comments and multi-line statements
        /// </summary>
        private List<string> ParseSqlStatements(string sql)
        {
            var statements = new List<string>();
            var currentStatement = new StringBuilder();
            var lines = sql.Split(new[] { '\r', '\n' }, StringSplitOptions.None);
            
            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                
                // Skip empty lines
                if (string.IsNullOrWhiteSpace(trimmedLine))
                    continue;
                
                // Skip pure comment lines (but preserve inline comments)
                if (trimmedLine.StartsWith("--"))
                    continue;
                
                // Add line to current statement
                currentStatement.AppendLine(line);
                
                // If line ends with semicolon, statement is complete
                if (trimmedLine.EndsWith(";"))
                {
                    var statement = currentStatement.ToString().Trim();
                    if (!string.IsNullOrWhiteSpace(statement))
                    {
                        statements.Add(statement);
                    }
                    currentStatement.Clear();
                }
            }
            
            // Add any remaining statement (shouldn't happen with proper SQL)
            if (currentStatement.Length > 0)
            {
                var statement = currentStatement.ToString().Trim();
                if (!string.IsNullOrWhiteSpace(statement))
                {
                    statements.Add(statement);
                }
            }
            
            return statements;
        }

        /// <summary>
        /// Begin a transaction (for bulk operations)
        /// </summary>
        public void BeginTransaction()
        {
            if (_currentTransaction != null)
                throw new InvalidOperationException("Transaction already in progress");

            _currentTransaction = _connection.BeginTransaction();
        }

        /// <summary>
        /// Commit the current transaction
        /// </summary>
        public async Task CommitAsync(CancellationToken ct = default)
        {
            if (_currentTransaction == null)
                throw new InvalidOperationException("No transaction in progress");

            _currentTransaction.Commit();
            _currentTransaction.Dispose();
            _currentTransaction = null;

            await Task.CompletedTask;
        }

        /// <summary>
        /// Rollback the current transaction
        /// </summary>
        public async Task RollbackAsync(CancellationToken ct = default)
        {
            if (_currentTransaction == null)
                throw new InvalidOperationException("No transaction in progress");

            _currentTransaction.Rollback();
            _currentTransaction.Dispose();
            _currentTransaction = null;

            await Task.CompletedTask;
        }

        public void Dispose()
        {
            if (_disposed) return;

            _currentTransaction?.Dispose();
            _packets?.Dispose();
            _frequencies?.Dispose();
            _players?.Dispose();
            _recording?.Dispose();
            _amplitudes?.Dispose(); // ? NEW: Dispose amplitude repository

            // Perform WAL checkpoint to merge WAL file back into main database
            // This ensures all file handles are released
            if (_connection.State == ConnectionState.Open)
            {
                try
                {
                    _connection.Execute("PRAGMA wal_checkpoint(TRUNCATE)");
                    Logger.Debug("WAL checkpoint completed");
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, "Failed to checkpoint WAL");
                }
                
                _connection.Close();
            }
            _connection.Dispose();

            _disposed = true;

            Logger.Debug($"SQLite Unit of Work disposed: {_filePath}");
        }
    }
}
