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

using System.IO;
using System.Security.Cryptography;

namespace FFXIV_TexTools
{
    class CreateDat
    {
        /// <summary>
        /// Creates .dat3 file
        /// </summary>
        public CreateDat()
        {
            // TODO: both of these assume we're only working with 040000
            //       textools currently only works with model textures
            //       UI textures are stored outside 040000 so it will need to be
            //       unhardcoded to work with those
            // TODO: this currently assumes that dat2 is the last official dat
            //       unhardcode it so that it's <last official dat> +1
            string newDat = Properties.Settings.Default.DefaultDir + "/040000.win32.dat3";
            // TODO: this only updates index(1), the same entry exists in index2
            string indexDir = Properties.Settings.Default.DefaultDir + "/040000.win32.index";

            #region Create new blank dat container
            using(FileStream fs = File.Create(newDat)){
                using (BinaryWriter bw = new BinaryWriter(fs))
                {
                    bw.BaseStream.Seek(0, SeekOrigin.Begin);

                    writeSqPackHeader(bw);
                    writeDatHeader(bw);
                }
            }
            #endregion

            #region Update the index 
            using(BinaryWriter bw = new BinaryWriter(File.OpenWrite(indexDir))){
                // Segment Header -> Segment 2 (Dat container metadata (?) ) -> Segment 2 number of dats
                bw.BaseStream.Seek(1104, SeekOrigin.Begin);
                // 040000 currently ships with 3 dats (dat0 -> dat2)
                // add one more to it and write it to the header
                byte b = 4;
                bw.Write(b);
                // NOTE: this does not update the SHA fields for the segment entry
            }
            #endregion

        }

        /// <summary>
        /// writes the sqpack to header
        /// </summary>
        /// <param name="bw"></param>
        public void writeSqPackHeader(BinaryWriter bw){
            byte[] header = new byte[1024];

            using (BinaryWriter hw = new BinaryWriter(new MemoryStream(header)))
            {
                hw.BaseStream.Seek(0, SeekOrigin.Begin);

                SHA1Managed shaM = new SHA1Managed();

                // SqPack Magic
                // PaSq
                hw.Write(1632661843);
                // ck
                hw.Write(27491);
                // Blank space after magic
                hw.Write(0);
                // Header Length
                hw.Write(1024);
                // Unk1
                hw.Write(1);
                // Type
                hw.Write(1);
                // Build date
                hw.Seek(8, SeekOrigin.Current);
                // Unk5
                hw.Write(-1);
                // Unk6
                // Unk7
                // ...
                // Blank
                hw.Seek(960, SeekOrigin.Begin);

                // SHA
                hw.Write(shaM.ComputeHash(header, 0, 959));

                bw.Write(header);
            }
        }

        /// <summary>
        /// Writes the new .dat header
        /// </summary>
        /// <param name="bw"></param>
        public void writeDatHeader(BinaryWriter bw)
        {
            byte[] header = new byte[1024];

            using (BinaryWriter hw = new BinaryWriter(new MemoryStream(header)))
            {
                hw.BaseStream.Seek(0, SeekOrigin.Begin);

                SHA1Managed shaM = new SHA1Managed();

                // Header Length
                hw.Write(header.Length);
                // Unk2
                hw.Write(0);
                // Unk3
                hw.Write(16);
                // Data Segment Length
                hw.Write(2048);
                // Dat is subcontainer
                hw.Write(2);
                // Unk6
                hw.Write(0);
                // Max File Size
                hw.Write(2000000000);
                // Unk8
                hw.Write(0);
                // ...
                // Blank
                hw.Seek(960, SeekOrigin.Begin);

                // SHA
                hw.Write(shaM.ComputeHash(header, 0, 959));

                bw.BaseStream.Seek(1024, SeekOrigin.Begin);
                bw.Write(header);
            }
        }

        /// <summary>
        /// Creates the empty modlist file
        /// </summary>
        public void createModList()
        {
            string modList = Properties.Settings.Default.DefaultDir + "/040000.modlist";
            File.Create(modList);
        }
    }
}
