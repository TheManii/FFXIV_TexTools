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
using System.Globalization;
using System.IO;
using System.IO.Compression;
using FFXIV_TexTools.Helpers;
using FFXIV_TexTools.Models;

namespace FFXIV_TexTools.IO
{
    // [Work in progress]
    public class Read3D
    {
        byte[] decompBytes;
        int meshCount, materialCount;

        /// <summary>
        /// Reads a Type-3 (model) file and converts it into a .obj file
        /// </summary>
        /// <param name="itemID">The item ID</param>
        /// <param name="raceID">The currently selected races ID</param>
        /// <param name="part">items equipment slot</param>
        /// <param name="parentNode">Equipment slot name</param>
        /// <param name="childNode">Item Name</param>
        /// FIXME: There is no return type (void)
        public Read3D(string itemID, string raceID, string part, string parentNode, string childNode)
        {
            #region Construct filename and get expanded offset
            string modelFolder, modelFile;

            modelFolder = "chara/equipment/e" + itemID + "/model";
            modelFile = "c" + raceID + "e" + itemID + "_" + part + ".mdl";

            // Get the expanded offset of the file
            FindOffset fo = new FindOffset(modelFile);
            string offset = fo.getFileOffset();

            int loc = ((int.Parse(offset, NumberStyles.HexNumber) / 8) & 0x000f) / 2;
            #endregion

            #region Extract the model
            // 040000 is hardcoded here simply because textools is not a generic
            // file manager, so we simply hardcode that value
            // TODO: Make this generic instead of hardcoding
            using (BinaryReader br = new BinaryReader(File.OpenRead(Properties.Settings.Default.DefaultDir + "/040000.win32.dat" + loc)))
            {
                // Parse expanded offset
                int initialOffset = int.Parse(offset, NumberStyles.HexNumber);

                #region Calculate physical offset
                // if (file.Contains(".dat1"))
                //{
                //  // The offset is for a file in 040000 .dat0
                //  initialOffset = initialOffset;
                //}
                if (loc == 1)
                {
                    // The offset is for a file in 040000 .dat1
                    initialOffset = initialOffset - 16;
                }
                else if (loc == 2)
                {
                    // The offset is for a file in 040000 .dat2
                    initialOffset = initialOffset - 32;

                }
                else if (loc == 3)
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

                List<byte> byteList = new List<byte>();

                #region Read the common header
                // Now that we have the correct subcontainer opened and the offset
                // seek to the offset and prepare to read the file entry
                br.BaseStream.Seek(initialOffset, SeekOrigin.Begin);

                // Begin reading the common header
                int headerLength = br.ReadInt32();
                // The type determines the block table layout
                // as we know ahead of time this is Type-3 (model)
                // we can assume ahead of time the block table used
                // (but not it's size, see below)
                int type = br.ReadInt32();
                int decompressedSize = br.ReadInt32();
                // This is actually
                // int commonHeaderUnk1 = br.ReadInt32();
                // int commonHeaderUnk2 = br.ReadInt32();
                // except that we don't know/use these
                // fields, so simply discard them
                br.ReadBytes(8);
                // This field is actually
                // int blockCount = br.ReadInt32()
                // in Type-2/4 (generic binary/texture)
                // but in Type-3 (model) it's unknown/unused
                int parts = br.ReadInt16();
                // br.ReadInt16();
                #endregion

                int endOfHeader = initialOffset + headerLength;

                // this value is unused
                int partCount = 0;

                // Add the header back to the file
                // The header is actually:
                // byte[] modelHeaderMagic = System.BitConverter.GetBytes(0x00020006);
                // int[] modelHeaderPadding = new byte[64];
                // byteList.AddRange(modelHeaderMagic);
                // byteList.AddRange(modelHeaderPadding);
                byteList.AddRange(new byte[68]);

                #region Model fields
                // Re-seek past the end of the common header, into the file itself
                // we also don't use the 1st field (header size) inside the file
                // so seek past that to the 1st fields we use
                // 24 is the length of the previous 6 fields
                br.BaseStream.Seek(initialOffset + 24, SeekOrigin.Begin);

                // 11 field groups is hardcoded
                // as we don't use blockCount above
                // instead the blockCount is dynamic
                // see totalBlocks for the actual block count
                int[] chunkUncompSizes = new int[11];
                int[] chunkLengths = new int[11];
                int[] chunkOffsets = new int[11];
                int[] chunkBlockStart = new int[11];
                int[] chunkNumBlocks = new int[11];

                /* Unlike Type-2/4 (generic binary/texture) block tables
                 * Type-3 (model) interleves it's fields:
                 * 
                 *  Type-2/4:
                 *      BlockEntry1a,1b,1c,1d
                 *                2a,2b,2d,2d
                 *                ...
                 *                Xa,Xb,Xc,Xd
                 *                Ya,Yb,Yc,Yd
                 *                
                 *  Type 3:
                 *      BlockEntry1a,2a,3a,4a
                 *                1b,2b,3b,4b
                 *                ...
                 *                Wa,Xa,Ya,Za
                 *                Wb,Xb,Yb,Zb
                 */
                
                for (int f = 0; f < 11; f++)
                {
                    chunkUncompSizes[f] = br.ReadInt32();
                }
                for (int f = 0; f < 11; f++)
                {
                    chunkLengths[f] = br.ReadInt32();
                }
                for (int f = 0; f < 11; f++)
                {
                    chunkOffsets[f] = br.ReadInt32();
                }
                for (int f = 0; f < 11; f++)
                {
                    chunkBlockStart[f] = br.ReadInt16();
                }
                int totalBlocks = 0;
                for (int f = 0; f < 11; f++)
                {
                    chunkNumBlocks[f] = br.ReadInt16();

                    totalBlocks += chunkNumBlocks[f];
                }

                meshCount = br.ReadInt16();
                materialCount = br.ReadInt16();

                br.ReadBytes(4);

                int[] blockSizes = new int[totalBlocks];

                for (int f = 0; f < totalBlocks; f++)
                {
                    blockSizes[f] = br.ReadInt16();
                }
                #endregion

                // Then we jump back to the compressed file contents
                br.BaseStream.Seek(initialOffset + headerLength + chunkOffsets[0], SeekOrigin.Begin);

                #region Read and decompress the blocks
                // i is the current block
                for(int i = 0; i < blockSizes.Length; i++)
                {
                    int lastPos = (int)br.BaseStream.Position;

                    #region Block fields
                    // This is actually
                    // int blockHeaderLength = br.ReadInt32();
                    // int blockHeaderUnk1   = br.ReadInt32();
                    // except that we don't know/use these
                    // fields, so simply discard them
                    br.ReadBytes(8);
                    int partCompressedSize = br.ReadInt32();
                    int partDecompressedSize = br.ReadInt32();
                    #endregion

                    #region Decompress the block
                    // If the size is 32000, that chunk is not compressed
                    // so directly read it in and move on to the next block
                    if (partCompressedSize == 32000)
                    {
                        byte[] forlist = br.ReadBytes(partDecompressedSize);
                        byteList.AddRange(forlist);
                    }
                    else
                    {
                        byte[] forlist = br.ReadBytes(partCompressedSize);
                        byte[] partDecompressedBytes = new byte[partDecompressedSize];

                        using (MemoryStream ms = new MemoryStream(forlist))
                        {
                            // The actual compressed data is compressed with deflate
                            using (DeflateStream ds = new DeflateStream(ms, CompressionMode.Decompress))
                            {
                                // Decompress partDecompressedSize's worth of bytes from ds
                                // and write them to partDecompressedBytes
                                int count = ds.Read(partDecompressedBytes, 0x00, partDecompressedSize);
                            }
                        }
                        // And save it
                        byteList.AddRange(partDecompressedBytes);
                    }
                    #endregion
                    br.BaseStream.Seek(lastPos + blockSizes[i], SeekOrigin.Begin);
                }
                #endregion
                // Store all the bytes, the file data is complete at this point
                decompBytes = byteList.ToArray();
            }
            #endregion

            #region Parse the model
            using (BinaryReader br = new BinaryReader(new MemoryStream(decompBytes)))
            {
                Model model = new Model(meshCount, materialCount);

                for(int x = 0; x < 3; x++)
                {
                    List<MeshInfo> mInfo = new List<MeshInfo>();

                    for (int i = 0; i < meshCount / 3; i++)
                    {
                        mInfo.Clear();
                        br.BaseStream.Seek((x * 136) + 68, SeekOrigin.Begin);
                        int dataArrayNum = br.ReadByte();

                        while (dataArrayNum != 255)
                        {
                            MeshInfo meshInfo = new MeshInfo(dataArrayNum, br.ReadByte(), br.ReadByte(), br.ReadByte());
                            mInfo.Add(meshInfo);
                            br.ReadBytes(4);
                            dataArrayNum = br.ReadByte();
                        }

                        model.Quality[x].meshInfoDict.Add(i, mInfo.ToArray());
                    }
                }

                br.BaseStream.Seek(136 * meshCount + 68, SeekOrigin.Begin);
                model.numStrings = br.ReadInt32();
                model.stringBlockSize = br.ReadInt32();

                br.ReadBytes(model.stringBlockSize);
                br.ReadBytes(4);

                model.numTotalMeshes = br.ReadInt16();
                model.numAtrStrings = br.ReadInt16();
                model.numParts = br.ReadInt16();
                model.numMaterialStrings = br.ReadInt16();
                model.numBoneStrings = br.ReadInt16();
                model.numBoneLists = br.ReadInt16();
                model.unk1 = br.ReadInt16();
                model.unk2 = br.ReadInt16();
                model.unk3 = br.ReadInt16();
                model.unk4 = br.ReadInt16();
                model.unk5 = br.ReadInt16();
                model.unk6 = br.ReadInt16();
                br.ReadBytes(10);
                model.unk7 = br.ReadInt16();
                br.ReadBytes(16);

                br.ReadBytes(32 * model.unk5);

                for(int i = 0; i < 3; i++)
                {
                    model.Quality[i].meshOffset = br.ReadInt16();
                    model.Quality[i].numMeshes = br.ReadInt16();

                    br.ReadBytes(40);

                    model.Quality[i].vertDataSize = br.ReadInt32();
                    model.Quality[i].indexDataSize = br.ReadInt32();
                    model.Quality[i].vertOffset = br.ReadInt32();
                    model.Quality[i].indexOffset = br.ReadInt32();
                }

                for(int x = 0; x < 3; x++)
                {
                    for (int i = 0; i < meshCount / 3; i++)
                    {
                        Mesh m = new Mesh();

                        m.numVerts = br.ReadInt32();
                        m.numIndex = br.ReadInt32();
                        m.materialNumber = br.ReadInt16();
                        m.partTableOffset = br.ReadInt16();
                        m.partTableCount = br.ReadInt16();
                        m.boneListIndex = br.ReadInt16();
                        m.indexDataOffset = br.ReadInt32();
                        for (int j = 0; j < 3; j++)
                        {
                            m.vertexDataOffsets[j] = br.ReadInt32();
                        }
                        for (int k = 0; k < 3; k++)
                        {
                            m.vertexSizes[k] = br.ReadByte();
                        }
                        m.numBuffers = br.ReadByte();

                        model.Quality[x].mesh[i] = m;
                    }
                }

                br.ReadBytes(model.numAtrStrings * 4);
                br.ReadBytes(model.unk6 * 20);

                model.setMeshParts();

                for(int i = 0; i < model.numParts; i++)
                {

                    MeshPart mp = new MeshPart();
                    mp.indexOffset = br.ReadInt32();
                    mp.indexCount = br.ReadInt32();
                    mp.attributes = br.ReadInt32();
                    mp.boneReferenceOffset = br.ReadInt16();
                    mp.boneReferenceCount = br.ReadInt16();

                    model.meshPart[i] = mp;
                }

                //something with attribute masks

                br.ReadBytes(model.unk7 * 12);
                br.ReadBytes(model.numMaterialStrings * 4);
                br.ReadBytes(model.numBoneStrings * 4);

                model.setBoneList();

                for(int i = 0; i < model.numBoneLists; i++)
                {
                    BoneList bl = new BoneList();
                    for(int j = 0; j < 64; j++)
                    {
                        bl.boneList[j] = br.ReadInt16();
                    }
                    bl.boneCount = br.ReadInt32();

                    model.boneList[i] = bl;
                }

                br.ReadBytes(model.unk1 * 16);
                br.ReadBytes(model.unk2 * 12);
                br.ReadBytes(model.unk3 * 4);

                model.boneIndexSize = br.ReadInt32();

                model.setBoneIndicies();

                for(int i = 0; i < model.boneIndexSize / 2; i++)
                {
                    model.boneIndicies[i] = br.ReadInt16();
                }

                int padding = br.ReadByte();
                br.ReadBytes(padding);

                for(int i = 0; i < model.bb.Length; i++)
                {
                    BoundingBoxes bb = new BoundingBoxes();
                    for (int j = 0; j < 4; j++)
                    {
                        bb.pointA[j] = br.ReadSingle();
                    }
                    for (int k = 0; k < 4; k++)
                    {
                        bb.pointB[k] = br.ReadSingle();
                    }

                    model.bb[i] = bb;

                }

                for(int i = 0; i < 3; i++)
                {
                    for(int j = 0; j < model.Quality[i].numMeshes; j++)
                    {
                        Mesh m = model.Quality[i].mesh[j];

                        m.setMeshData();

                        for(int k = 0; k < m.numBuffers; k++)
                        {
                            br.BaseStream.Seek(model.Quality[i].vertOffset + m.vertexDataOffsets[k], SeekOrigin.Begin);

                            MeshData md = new MeshData();
                            md.meshData = br.ReadBytes(m.vertexSizes[k] * m.numVerts);

                            m.meshData[k] = md;
                        }

                        br.BaseStream.Seek(model.Quality[i].indexOffset + (m.indexDataOffset * 2), SeekOrigin.Begin);

                        m.indexData = br.ReadBytes(2 * m.numIndex);
                    }

                }

                List<string> objBytes = new List<string>();

                int vertexs = 0, coordinates = 0, normals = 0;

                for (int i = 0; i < model.Quality[0].numMeshes; i++)
                {
                    objBytes.Clear();
                    Mesh m = model.Quality[0].mesh[i];

                    MeshInfo[] mi = model.Quality[0].meshInfoDict[i];

                    int c = 0;
                    foreach(var a in mi)
                    {
                        if(a.useType == 0)
                        {
                            vertexs = c;
                        }
                        else if(a.useType == 3)
                        {
                            normals = c;
                        }
                        else if (a.useType == 4)
                        {
                            coordinates = c;
                        }
                        c++;
                    }

                    using (BinaryReader br1 = new BinaryReader(new MemoryStream(m.meshData[mi[vertexs].dataArrayNum].meshData)))
                    {
                        for(int j = 0; j < m.numVerts; j++)
                        {
                            int offset1 = j * m.vertexSizes[mi[vertexs].dataArrayNum] + mi[vertexs].offset;
                            br1.BaseStream.Seek(offset1, SeekOrigin.Begin);

                            if(mi[vertexs].dataType == 13 || mi[vertexs].dataType == 14)
                            {
                                float f1, f2, f3;

                                Half h1 = Half.ToHalf((ushort)br1.ReadInt16());
                                Half h2 = Half.ToHalf((ushort)br1.ReadInt16());
                                Half h3 = Half.ToHalf((ushort)br1.ReadInt16());


                                f1 = HalfHelper.HalfToSingle(h1);
                                f2 = HalfHelper.HalfToSingle(h2);
                                f3 = HalfHelper.HalfToSingle(h3);

                                objBytes.Add("v " + f1.ToString() + " " + f2.ToString() + " " + f3.ToString() + " ");
                            }
                            else if(mi[vertexs].dataType == 2)
                            {
                                float f1, f2, f3;

                                f1 = br1.ReadSingle();
                                f2 = br1.ReadSingle();
                                f3 = br1.ReadSingle();

                                objBytes.Add("v " + f1.ToString() + " " + f2.ToString() + " " + f3.ToString() + " ");
                            }
                        }
                    }

                    using (BinaryReader br1 = new BinaryReader(new MemoryStream(m.meshData[mi[coordinates].dataArrayNum].meshData)))
                    {
                        for(int j = 0; j < m.numVerts; j++)
                        {

                            int offset1 = j * m.vertexSizes[mi[coordinates].dataArrayNum] + mi[coordinates].offset;

                            br1.BaseStream.Seek(offset1, SeekOrigin.Begin);

                            Half a1 = Half.ToHalf((ushort)br1.ReadInt16());
                            Half b1 = Half.ToHalf((ushort)br1.ReadInt16());

                            float a = HalfHelper.HalfToSingle(a1);
                            float b = (HalfHelper.HalfToSingle(b1));

                            objBytes.Add("vt " + a.ToString() + " " + b.ToString() + " ");
                        }
                    }

                    using (BinaryReader br1 = new BinaryReader(new MemoryStream(m.meshData[mi[normals].dataArrayNum].meshData)))
                    {
                        for (int j = 0; j < m.numVerts; j++)
                        {
                            br1.BaseStream.Seek(j * m.vertexSizes[mi[normals].dataArrayNum] + mi[normals].offset, SeekOrigin.Begin);

                            Half h1 = Half.ToHalf((ushort)br1.ReadInt16());
                            Half h2 = Half.ToHalf((ushort)br1.ReadInt16());
                            Half h3 = Half.ToHalf((ushort)br1.ReadInt16());

                            objBytes.Add("vn " + HalfHelper.HalfToSingle(h1).ToString() + " " + HalfHelper.HalfToSingle(h2).ToString() + " " + HalfHelper.HalfToSingle(h3).ToString() + " ");
                        }
                    }

                    using (BinaryReader br1 = new BinaryReader(new MemoryStream(m.indexData)))
                    {
                        for (int j = 0; j < m.numIndex; j += 3)
                        {
                            int a1 = br1.ReadInt16() + 1;
                            int b1 = br1.ReadInt16() + 1;
                            int c1 = br1.ReadInt16() + 1;

                            objBytes.Add("f " + a1 + "/" + a1 + "/" + a1 + " " + b1 + "/" + b1 + "/" + b1 + " " + c1 + "/" + c1 + "/" + c1 + " ");
                        }
                    }

                    Directory.CreateDirectory(Properties.Settings.Default.SaveDir + "/" + parentNode + "/" + childNode);
                    File.WriteAllLines(Properties.Settings.Default.SaveDir + "/" + parentNode + "/" + childNode + "/Mesh_" + i + ".obj", objBytes.ToArray());
                }
            }
            #endregion
        } 
    }
}
