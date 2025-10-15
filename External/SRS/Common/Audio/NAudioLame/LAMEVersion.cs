namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.NAudioLame;

/// <summary>LAME DLL version information</summary>
public class LAMEVersion
{
	/// <summary>Constructor, library-local, converts <see cref="LameWrapper.LAMEVersion" /></summary>
	/// <param name="source"></param>
	internal LAMEVersion(LameWrapper.LAMEVersion source)
    {
        Major = source.major;
        Minor = source.minor;
        Alpha = source.alpha;
        Beta = source.beta;

        PsychoAcoustic_Major = source.psy_major;
        PsychoAcoustic_Minor = source.psy_minor;
        PsychoAcoustic_Alpha = source.psy_alpha;
        PsychoAcoustic_Beta = source.psy_beta;

        Features = source.features;
    }

    // Prevent default construction
    private LAMEVersion()
    {
    }
    /* generic LAME version */

    /// <summary>LAME library major version</summary>
    public int Major { get; private set; }

    /// <summary>LAME library minor version</summary>
    public int Minor { get; private set; }

    /// <summary>LAME library 'Alpha' version flag</summary>
    public bool Alpha { get; private set; }

    /// <summary>LAME library 'Beta' version flag</summary>
    public bool Beta { get; private set; }

    /// <summary>Psychoacoustic code major version</summary>
    public int PsychoAcoustic_Major { get; private set; }

    /// <summary>Psychoacoustic code minor version</summary>
    public int PsychoAcoustic_Minor { get; private set; }

    /// <summary>Psychoacoustic code 'Alpha' version flag</summary>
    public bool PsychoAcoustic_Alpha { get; private set; }

    /// <summary>Psychoacoustic code 'Beta' version flag</summary>
    public bool PsychoAcoustic_Beta { get; private set; }

    /// <summary>Compile-time features string</summary>
    public string Features { get; private set; }
}