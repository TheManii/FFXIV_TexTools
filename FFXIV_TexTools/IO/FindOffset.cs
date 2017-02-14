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
    public class FindOffset
    {
        FFCRC crc = new FFCRC();

        /// <summary>
        /// an offset stored as a big-endian hexadecimal value
        /// </summary>
        string fileOffset = "0";
        /// <summary>
        /// a CRC-32-FFXIV-BE hash
        /// </summary>
        string fileCRC;
        List<int> races;

        /// <summary>
        /// Finds the offset of a texture or model
        /// </summary>
        /// <param name="textureName">A string containing the filename of a texture/model (or a CRC32-FFXIV-BE hash)</param>
        public FindOffset(string textureName)
        {
            // If the incoming string contains an extension, then it's an actual path and not an offset
            // FIXME: make check generic by using System.IO.Path.HasExtension(string)
            //          the above will also allow it to handle abritrary files, the current implementation
            //          specifically whitelists valid file types, 
            //          even though the code below it is (mostly) file format agnostic
            if (textureName.Contains(".tex") || textureName.Contains(".mdl"))
            {
                // If it is a path, get the hash from it, and use a dummy foldername for it
                // Example: If "foo.tex" returned a hash of "01234567", pad it to "0123456700000000"
                // The code below searches index1, which stores file entries as:
                //
                // (Entry X): AAAAAAAABBBBBBBBXXXXXXXX00000000 
                // (Entry Y): AAAAAAAABBBBBBBBXXXXXXXX00000000 
                //
                // AAAAAAAA : hash of the filename
                // BBBBBBBB : hash of the foldername
                // XXXXXXXX : packed offset
                // 00000000 : Null
                //
                // Since we don't care about the foldername, simply pad it with 0's
                // instead of seeking around it
                fileCRC = crc.text(textureName).PadLeft(8, '0');
            }
            else
            {
                // If it is a hash, just use it directly
                fileCRC = textureName;
            }

            // Note that textools can only handle specific filetypes by design
            // the limit here to 040000 is artifical since the other containers has no data textools can currently handle
            using (BinaryReader br = new BinaryReader(File.OpenRead(Properties.Settings.Default.DefaultDir + "/040000.win32.index")))
            {
                br.BaseStream.Seek(1036, SeekOrigin.Begin);
                // This is the upper bound for how far to check the index
                // past this are the folder section of index
                int totalFiles = br.ReadInt32();

                // the actual beginning of the files is another 4 bytes after this
                // but don't include it here so the for loop can do it
                br.BaseStream.Seek(2048, SeekOrigin.Begin);

                // the br.ReadBytes(4) in the for loop decleration is the null portion of the index1 entry
                for (int i = 0; i < totalFiles; br.ReadBytes(4), i += 16)
                {
                    // Load the current file entry, the underlying filename is a little endian CRC32-FFXIV hash
                    // ReadInt32 reads the underlying bytes and returns an int
                    // ToString and PadLeft turns this into a big endian string
                    string tempOffset = br.ReadInt32().ToString("X").PadLeft(8, '0');

                    // Since both tempOffset and fileCRC are now big endian hex strings, compare them
                    if (tempOffset.Equals(fileCRC))
                    {
                        // read the folder field and discard it
                        br.ReadBytes(4);
                        // read the packed offset field
                        byte[] offset = br.ReadBytes(4);
                        // read the actual offset and construct a big endian hexadecimal string
                        fileOffset = (BitConverter.ToInt32(offset, 0) * 8).ToString("X").PadLeft(8, '0');
                        // exit
                        break;
                    }
                    else
                    {
                        // if they don't match seek past the folder/offset fields
                        // the null field is handled in the for loop decleration
                        br.ReadBytes(8);
                    }
                }
            }
        }

        /// <summary>
        /// Find the offset of a file
        /// </summary>
        /// See FileOffset(string) for comments on how this works, it's mostly identical
        /// <param name="textureHex">The CRC32-FFXIV-BE hash of a file</param>
        /// <param name="folderHex">The CRC32-FFXIV-BE hash of a folder</param>
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
        /// Find the offset of multiple files in the same folder,
        /// also loads race data(?)
        /// </summary>
        /// not currntly in use
        /// <param name="textureHexs">The CRC32-FFXIV-BE hash of an array of files</param>
        /// <param name="folderHex">The CRC32-FFXIV-BE hash of a folder</param>
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
        /// The actual file offset from a previous FindOffset()
        /// </summary>
        /// <returns>A string containing the offset as big-endian hexadecimal</returns>
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
