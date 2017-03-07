// FFXIV TexTools
// Copyright © 2017 Rafael Gonzalez - All Rights Reserved
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
using System.Globalization;
using System.IO;
using System.IO.Compression;

namespace FFXIV_TexTools.IO
{
    class TextureReader
    {
        byte[] decompressedTexture;
        int textureType;
        int[] dimensions = new int[2];


        /// <summary>
        /// Reads reads a Type-4 (texture) file
        /// <para>Use FFXIV_TexTools.TextureReader.getDecompressedTexture() to get the contents</para>
        /// </summary>
        /// <param name="offset">An Expanded offset to the texture</param>
        /// <param name="file">The full path to the dat subcontainer</param>
        public TextureReader(string offset, string file)
        {
            List <byte> byteList = new List<byte>();

            using (BinaryReader br = new BinaryReader(File.OpenRead(file)))
            {
                // Parse expanded offset
                int initialOffset = int.Parse(offset, NumberStyles.HexNumber);

                #region Calculate physical offset
                // if (file.Contains(".dat0"))
                //{
                //  // The offset is for a file in 040000 .dat0
                //  initialOffset = initialOffset;
                //}
                if (file.Contains(".dat1"))
                {
                    // The offset is for a file in 040000 .dat1
                    initialOffset = initialOffset - 16;
                }
                else if (file.Contains(".dat2"))
                {
                    // The offset is for a file in 040000 .dat2
                    initialOffset = initialOffset - 32;

                }
                else if (file.Contains(".dat3"))
                {
                    // The offset is for a file in 040000 .dat3
                    initialOffset = initialOffset - 48;
                }
                // else if (file.Contains(".dat4"))
                //{
                //  // The offset is for a file in 040000 .dat4
                //  initialOffset = initialOffset - 64;
                //}
                // etc...

                // at this point, initialOffset contains the Physical offset of the file
                // and loc is it's container, which means we have everything we need to start
                // reading the file entry's header and contents
                #endregion

                #region Read the common header
                // Now that we have the correct subcontainer opened and the offset
                // seek to the offset and prepare to read the file entry
                br.BaseStream.Seek(initialOffset, SeekOrigin.Begin);

                // Begin reading the common header
                int headerLength = br.ReadInt32();
                // The type determines the block table layout
                // as we know ahead of time this is Type-4 (texture)
                // we can assume ahead of time the block table used
                // (but not it's size, see below)
                int type = br.ReadInt32();
                // The size of the decompressed file (in bytes)
                int decompSize = br.ReadInt32();
                // This is actually
                // int commonHeaderUnk1 = br.ReadInt32();
                // int commonHeaderUnk2 = br.ReadInt32();
                // except that we don't know/use these
                // fields, so simply discard them
                br.ReadBytes(8);

                // The block count in the block table is determined here
                int mipMapCount = br.ReadInt32();
                #endregion

                int endOfHeader = initialOffset + headerLength;

                // 24 is the length of the previous 6 fields
                int mipMapInfoStart = initialOffset + 24;

                #region Texture header
                // Re-seek past the end of the common header, into the file itself
                // we also don't use the 1st field (header size) inside the file
                // so seek past that to the 1st fields we use
                br.BaseStream.Seek(endOfHeader + 4, SeekOrigin.Begin);

                // these fields are not actually used to decompress
                // the file, but to actually render it
                // TODO: instead of mixing file decompression and rendering
                //       save the entire file header and handle the entire
                //       decompressed file as a whole, which will also
                //       simplify importing actual .tex files, instead
                //       of only having .dxt as the storage file type
                textureType = br.ReadInt32();
                int width = br.ReadInt16();
                int height = br.ReadInt16();
                dimensions[0] = width;
                dimensions[1] = height;

                // there are actually more fields inside the tex header
                // but we simply discard them as textools directly works
                // with main texture chunks + mipmap chunks instead of
                // whole tex files
                #endregion

                /* The reconstituted tex file would look like:
                 * 
                 * <Texture header> - copied as-is
                 * <decompressed mipmap 1 part A>
                 * <decompressed mipmap 1 part B>
                 * <decompressed mipmap 1 part ...>
                 * <decompressed mipmap 1 part M>
                 * <decompressed mipmap 2 part A>
                 * <decompressed mipmap 2 part B>
                 * <decompressed mipmap 2 part ...>
                 * <decompressed mipmap 2 part N>
                 * <decompressed mipmap P part S>
                 * <decompressed mipmap Q part T>
                 * <decompressed mipmap R part U>
                 * <decompressed mipmap X>
                 * <decompressed mipmap Y>
                 * <decompressed mipmap Z>
                 */

                #region Read and decompress the blocks
                // i is the current block
                // j is the offset entry in the common header for block[i]
                for (int i = 0, j = 0; i < mipMapCount; i++)
                {
                    // We jump back to the common header 
                    // and read the entry for the current block
                    br.BaseStream.Seek(mipMapInfoStart + j, SeekOrigin.Begin);

                    #region Common Header Block fields
                    int offsetFromHeaderEnd = br.ReadInt32();
                    int mipMapLength = br.ReadInt32();
                    int mipMapSize = br.ReadInt32();
                    int mipMapStart = br.ReadInt32();
                    // Each individual block can be made of multiple parts
                    int mipMapParts = br.ReadInt32();

                    // There's also
                    // blockCompressedSize   = br.ReadInt16();
                    // blockDecompressedSize = br.ReadInt16();
                    // except that we don't use these
                    // fields, so simply discard them
                    #endregion

                    int mipMapOffset = endOfHeader + offsetFromHeaderEnd;

                    // Then we jump back to the compressed file contents
                    br.BaseStream.Seek(mipMapOffset, SeekOrigin.Begin);

                    #region Block header
                    // This is actually
                    // int blockHeaderLength = br.ReadInt32();
                    // int blockHeaderUnk1   = br.ReadInt32();
                    // except that we don't know/use these
                    // fields, so simply discard them
                    br.ReadBytes(8);
                    int compressedSize = br.ReadInt32();
                    int decompressedSize = br.ReadInt32();
                    #endregion

                    #region Blocks with multiple parts
                    if (mipMapParts > 1)
                    {
                        byte[] compressedData = br.ReadBytes(compressedSize);
                        byte[] decompressedData = new byte[decompressedSize];

                        // Decompress Block[i] Part[0]
                        using (MemoryStream ms = new MemoryStream(compressedData))
                        {
                            // The actual compressed data is compressed with deflate
                            using (DeflateStream ds = new DeflateStream(ms, CompressionMode.Decompress))
                            {
                                // Decompress decompressedSize's worth of bytes from ds
                                // and write them to decompressedData
                                ds.Read(decompressedData, 0x00, decompressedSize);
                            }
                        }

                        // And save it
                        byteList.AddRange(decompressedData);

                        // Decompress Block[i] Part[1] -> Part[k]
                        for (int k = 1; k < mipMapParts; k++)
                        {
                            // Seek past the padding at the end of the file to the next header
                            // The padding on files actually ends on the nearest 0x70 or 0xF0th
                            // offset (whichever is closer), the next field would be
                            // the header of the next part, and the first field in that is the
                            // size of the header, which is 0x10
                            // we check byte by byte as actual contents do not necessarily
                            // end on a multiple of 4
                            // TODO: figure out a way to simply calculate the nearest 0x70th or
                            //       0xF0th offset (rounded up) instead of reading byte by byte
                            byte check = br.ReadByte();
                            while (check != 0x10)
                            {
                                check = br.ReadByte();
                            }

                            // ReadByte()  : (above)
                            // ReadBytes(3): The rest of the part header length
                            // ReadInt32() : Chunk header Unk1
                            br.ReadBytes(7);
                            compressedSize = br.ReadInt32();
                            decompressedSize = br.ReadInt32();

                            compressedData = br.ReadBytes(compressedSize);
                            decompressedData = new byte[decompressedSize];

                            // Decompress a block part
                            using (MemoryStream ms = new MemoryStream(compressedData))
                            {
                                // The actual compressed data is compressed with deflate
                                using (DeflateStream ds = new DeflateStream(ms, CompressionMode.Decompress))
                                {
                                    // Decompress decompressedSize's worth of bytes from ds
                                    // and write them to decompressedData
                                    ds.Read(decompressedData, 0x00, decompressedSize);
                                }
                            }
                            byteList.AddRange(decompressedData);
                        }
                    }
                    #endregion
                    #region Blocks with one part
                    else
                    {
                        byte[] compressedData, decompressedData;

                        // If the size is 32000, that chunk is not compressed
                        // so directly read it in and move on to the next block
                        if (compressedSize != 32000)
                        {
                            compressedData = br.ReadBytes(compressedSize);
                            decompressedData = new byte[decompressedSize];

                            using (MemoryStream ms = new MemoryStream(compressedData))
                            {
                                using (DeflateStream ds = new DeflateStream(ms, CompressionMode.Decompress))
                                {
                                    ds.Read(decompressedData, 0x00, decompressedSize);
                                }
                            }

                            byteList.AddRange(decompressedData);
                        }
                        else
                        {
                            decompressedData = br.ReadBytes(decompressedSize);
                            byteList.AddRange(decompressedData);
                        }
                    }
                    #endregion
                    j = j + 20;
                }
                #endregion
                if (byteList.Count < decompSize)
                {
                    int difference = decompSize - byteList.Count;
                    byte[] padd = new byte[difference];
                    Array.Clear(padd, 0, difference);
                    byteList.AddRange(padd);
                }
            }
            // Store all the bytes, the file data is complete at this point
            decompressedTexture = byteList.ToArray();
        }

        public byte[] getDecompressedTexture()
        {
            return decompressedTexture;
        }

        public int getTextureType()
        {
            return textureType;
        }

        public int[] getDimensions()
        {
            return dimensions;
        }
    }
}
