// FFXIV TexTools
// Copyright © 2017 Rafael Gonzalez - All Rights Reserved
// Copyright © 2017 TheManii, et al. - All Rights Reserved
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FFXIV_TexTools.Helpers;

namespace FFXIV_TexTools.IO
{
    /// <summary>
    /// String conversion functions used to map filenames to physical locations
    /// </summary>
    /// FIXME?: Does this need to be non static?
    /// FindOffset can be refactored into a static class
    public class FindOffset
    {
        /// <summary>
        /// Create a FFCRC object, as FFCRC.text() is needed below
        /// </summary>
        FFCRC crc = new FFCRC();

        /// <summary>
        /// The temporary offset in question
        /// </summary>
        string fileOffset = "0";
        /// <summary>
        /// a CRC32-FFXIV hash
        /// </summary>
        string fileCRC;
        List<int> races;

        /// <summary>
        /// Finds the stored offset of a given path in 400000.win32.index(1), does not check which folder a file is in
        /// <para>
        /// Use FindOffset.getFileOffset() to get the resulting offset and for more information on offsets
        /// </para>
        /// </summary>
        /// FIXME: This function overlaps with FindOffset(string, string)
        /// <param name="textureName">A string containing the filename of a texture/model (or a CRC32-FFXIV hash)</param>
        public FindOffset(string textureName)
        {
            // If the incoming string contains an extension
            // it must be a file and we need the hash of it
            // FIXME: this should be refactored to handle generic filenames
            //        or hashes, all hashes should be calculated at the same place/point
            if (textureName.Contains(".tex") || textureName.Contains(".mdl"))
            {
                
                fileCRC = crc.text(textureName).PadLeft(8, '0');
            }
            else
            {
                // If it is a hash, just use it directly
                fileCRC = textureName;
            }

            // Note that textools can only handle specific filetypes by design
            // the limit here to 040000 is artifical since the other containers has no data textools can currently handle
            // FIXME: if future indexes are to be read, this needs to be un-hardcoded, and checks to see if the file exists
            using (BinaryReader br = new BinaryReader(File.OpenRead(Properties.Settings.Default.DefaultDir + "/040000.win32.index")))
            {
                // 1036 (0x040c) is: Master Header Size ->  Segment Header Origin -> Segment 1 Unk  ->  Segment 1 Origin -> Segment 1 Length
                // Offset (size)   : 0x000c (int32)     ->  0x0400  (int32)       -> 0x0404 (int32) ->  0x0408 (int32)   -> 0x040c (int32)
                // Current Value   : 0x0400             ->  0x0400                -> (Unneeded)     ->  0x0800           -> 0x01ab950 
                // Note: values may change depending on game version, bytes are stored as little-endian (the above is big-endian)
                br.BaseStream.Seek(1036, SeekOrigin.Begin);
                // FIXME: This is more correctly the size of segment 1
                //        Each entry in segment 1 is 16 bytes
                int totalFiles = br.ReadInt32();

                // Seek to the beginning of segment 1's data, which is 2048 (0x800) as seen above
                br.BaseStream.Seek(2048, SeekOrigin.Begin);

                // i+= 16 is to take into account the size of an entry
                // the br.ReadBytes(4) here handles reading past the null field
                for (int i = 0; i < totalFiles; br.ReadBytes(4), i += 16)
                {
                    // Load the current file entry, and turn it into a string to compare against
                    string tempOffset = br.ReadInt32().ToString("X").PadLeft(8, '0');

                    // Actually compare them
                    if (tempOffset.Equals(fileCRC))
                    {
                        // read the folder field and discard it
                        // FIXME: since the folder field is discarded, it's possible
                        //        to have filename collisions in theory;
                        //        in practice: that should not be an issue in TexTools
                        // FIXME: Segment 4 (folder offsets) is not used, while it may/may not
                        //        be faster to not use them, it might be more correct to actually use them
                        br.ReadBytes(4);
                        // read the packed offset field
                        byte[] offset = br.ReadBytes(4);
                        // read the actual offset and construct a string
                        fileOffset = (BitConverter.ToInt32(offset, 0) * 8).ToString("X").PadLeft(8, '0');
                        // exit the loop
                        break;
                    }
                    else
                    {
                        // If they don't match, read past the folder/offset fields
                        // the null field is handled in the for loop decleration
                        br.ReadBytes(8);
                    }
                }
            }
        }

        /// <summary>
        /// Finds the stored offset of a given path in 400000.win32.index(1), also checks which folder a file is in
        /// <para>
        /// Use FindOffset.getFileOffset() to get the resulting offset and for more information on offsets
        /// </para>
        /// </summary>
        /// See FileOffset(string) for comments on how this works, it's mostly identical
        /// <param name="textureHex">The CRC32-FFXIV hash of a file</param>
        /// <param name="folderHex">The CRC32-FFXIV hash of a folder</param>
        public FindOffset(string textureHex, string folderHex)
        {
            using (BinaryReader br = new BinaryReader(File.OpenRead(Properties.Settings.Default.DefaultDir + "/040000.win32.index")))
            {
                br.BaseStream.Seek(1036, SeekOrigin.Begin);
                int totalFiles = br.ReadInt32();

                br.BaseStream.Seek(2048, SeekOrigin.Begin);
                for (int i = 0; i < totalFiles; br.ReadBytes(4), i += 16)
                {
                    string tempOffset = br.ReadInt32().ToString("X").PadLeft(8, '0');

                    if (tempOffset.Equals(textureHex))
                    {
                        // Check the foldername, if the caller specified FindOffset("a file", "none")
                        // then drop the foldername and simply return the file offset
                        // FIXME: this is effectively what FindOffset(string) does
                        if (folderHex.Equals("none"))
                        {
                            br.ReadBytes(4);
                            byte[] offset = br.ReadBytes(4);
                            fileOffset = (BitConverter.ToUInt32(offset, 0) * 8).ToString("X").PadLeft(8, '0');
                            break;
                        }

                        string foHex = br.ReadInt32().ToString("X").PadLeft(8, '0');

                        // Otherwise, actually check that the foldername matches
                        if (foHex.Equals(folderHex))
                        {
                            byte[] offset = br.ReadBytes(4);
                            fileOffset = (BitConverter.ToUInt32(offset, 0) * 8).ToString("X").PadLeft(8, '0');
                            break;
                        }
                        else
                        {
                            br.ReadBytes(4);
                        }
                    }
                    else
                    {
                        br.ReadBytes(8);
                    }
                }
            }
        }

        /// <summary>
        /// Find the stored offsets of multiple files in the same folder,
        /// also loads race data(?)
        /// <para>
        /// Use FindOffset.getFileOffset() to get the resulting offset and for more information on offsets
        /// </para>
        /// </summary>
        /// not currntly in use
        /// See FileOffset(string) for comments on how this works, it's mostly identical
        /// <param name="textureHexs">The CRC32-FFXIV hash of an array of files</param>
        /// <param name="folderHex">The CRC32-FFXIV hash of a folder</param>
        public FindOffset(string[] textureHexs, string folderHex)
        {
            using (BinaryReader br = new BinaryReader(File.OpenRead(Properties.Settings.Default.DefaultDir + "/040000.win32.index")))
            {
                br.BaseStream.Seek(1036, SeekOrigin.Begin);
                int totalFiles = br.ReadInt32();

                br.BaseStream.Seek(2048, SeekOrigin.Begin);
                for (int i = 0; i < totalFiles; br.ReadBytes(4), i += 16)
                {
                    string tempOffset = br.ReadInt32().ToString("X").PadLeft(8, '0');

                    if (textureHexs.Contains(tempOffset))
                    {
                        string foHex = br.ReadInt32().ToString("X").PadLeft(8, '0');

                        if (foHex.Equals(folderHex))
                        {
                            byte[] offset = br.ReadBytes(4);
                            fileOffset = (BitConverter.ToUInt32(offset, 0) * 8).ToString("X").PadLeft(8, '0');
                            races.Add(Array.IndexOf(textureHexs, tempOffset));
                        }
                        else
                        {
                            br.ReadBytes(4);
                        }
                    }
                    else
                    {
                        br.ReadBytes(8);
                    }
                }
            }
        }
        /// <summary>
        /// The expanded offset for an entry in index1 from FindOffset()
        /// <para>
        /// There are three kinds of offsets (both index1 and index2 use the same formula, but different source paths):
        /// </para>
        /// <para/> Stored offsets: What is stored in the index, an expanded offset divided by 8
        /// <para/> Expanded offsets: The physical offset with the dat container muxed in
        /// <para/> Physical offsets: The actual physical location offset of a file in the dat container
        /// </summary>
        /// <returns>A string containing the expanded offset as big-endian hexadecimal</returns>
        public string getFileOffset()
        {
            return fileOffset;
        }

        public int[] getRaces()
        {
            return races.ToArray();
        }
    }
}
