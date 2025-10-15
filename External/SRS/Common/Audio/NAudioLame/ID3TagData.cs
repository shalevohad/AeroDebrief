using System;
using System.Collections.Generic;
using System.Linq;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common.Audio.NAudioLame;

/// <summary>ID3 tag content</summary>
public class ID3TagData
{
    /// <summary>Album (TALB)</summary>
    public string Album;

    /// <summary>Album art - PNG, JPG or GIF file content</summary>
    public byte[] AlbumArt;

    /// <summary>AlbumArtist (TPE2)</summary>
    public string AlbumArtist;

    /// <summary>Artist (TPE1)</summary>
    public string Artist;

    /// <summary>Comment (COMM)</summary>
    public string Comment;

    /// <summary>Genre (TCON)</summary>
    public string Genre;

    // Experimental:
    /// <summary>Subtitle (TIT3)</summary>
    public string Subtitle;

    // Standard values:
    /// <summary>Track title (TIT2)</summary>
    public string Title;

    /// <summary>Track number (TRCK)</summary>
    public string Track;

    /// <summary>Year (TYER)</summary>
    public string Year;

    /// <summary>
    ///     User defined text frames (TXXX) - Multiples are allowed as long as their description is unique (Format :
    ///     "description=text")
    /// </summary>
    /// <remarks>
    ///     Obsolete.  Please use <see cref="UserDefinedText" /> property instead.
    ///     Implemented as accessor to <see cref="UserDefinedText" /> Dictionary.
    ///     If multiple tags with the same description are supplied only the last one is used.
    /// </remarks>
    [Obsolete("Use the UserDefinedText property instead.", false)]
    public string[] UserDefinedTags
    {
        get => UserDefinedText.Select(kv => $"{kv.Key}={kv.Value}").ToArray();
        set => SetUDT(value);
    }

    /// <summary>User defined text frames (TXXX)</summary>
    /// <remarks>Stored in ID3v2 tag as one TXXX frame per item.</remarks>
    public Dictionary<string, string> UserDefinedText { get; } = new();

    /// <summary>
    ///     Clear <see cref="UserDefinedText" /> and insret values from collection of "description=text" strings.
    /// </summary>
    /// <param name="data">Collection to load.</param>
    public void SetUDT(IEnumerable<string> data)
    {
        UserDefinedText.Clear();
        foreach (var item in data)
        {
            var key = item.Split('=').First();
            var valuePos = key.Length + 1;
            var val = valuePos > item.Length ? string.Empty : item.Substring(valuePos);
            UserDefinedText[key] = val;
        }
    }
}