using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Twinkly_xled.JSONModels;

namespace TwinklyWPF.Utilities
{
    public class Layouts
    {
        public static void InitializeDefaultLayout(XYZ[] coordinates, int startOffset, int n)
        {
            const double s = 0.3;   // default spacing
            double startx = (startOffset % 600 + 10) * s;
            var xyz = new XYZ { x = startx, y = 2.0, z = 0.0 };
            for (int j = 0; j < n; ++j)
            {
                coordinates[j + startOffset] = xyz;
                xyz.x += s;
            }
        }

        public static void InitializeHouseLayout(XYZ[] coordinates, int startOffset)
        {
            Debug.Assert(coordinates.Length >= startOffset + 600);


            int zone = 0;
            var xyz = new XYZ { x = 0, y = 3.9, z = 0.0 };
            const double s = 0.1;   // default spacing

            for (int j = 0, zi = 0; j < 600; ++j, ++zi)
            {
                switch (j)
                {
                    case 90: ++zone; xyz.z = zone; zi = 0; break;  // 1: short run
                    case 136: ++zone; xyz.z = zone; zi = 0; break;  // 2: main
                    case 187: ++zone; xyz.z = zone; zi = 0; break;  // 3: dead
                    case 198: ++zone; xyz.z = zone; zi = 0; break;  // 4: doorframe L down
                    case 224: ++zone; xyz.z = zone; zi = 0; break;  // 5: doorframe L up
                    case 248: ++zone; xyz.z = zone; zi = 0; break;  // 6: doorframe top
                    case 271: ++zone; xyz.z = zone; zi = 0; break;  // 7: doorframe R down
                    case 300: ++zone; xyz.z = zone; zi = 0; xyz = new XYZ { x = 0.5 * s, y = 3.9 - 0.5 * s, z = 5.0 }; break; // string 2
                    case 395: ++zone; xyz.z = zone; zi = 0; break;  // 9: short run
                    case 443: ++zone; xyz.z = zone; zi = 0; break;  // 10: main
                    case 557: ++zone; xyz.z = zone; zi = 0; break;  // 11: leftover/downspout
                    case 571: ++zone; xyz.z = zone; zi = 0; break;  // 12: downspout
                }

                coordinates[j + startOffset] = xyz;

                switch (zone)
                {
                    case 0: // back main #1
                    case 1:
                    case 2:
                        xyz.x += s;
                        break;
                    case 3: // link to doorframe (always off)
                        xyz.x += 0.8 * s;
                        xyz.y -= 0.5 * s;
                        break;
                    case 4: // doorframe L down
                        xyz.y -= s;
                        break;
                    case 5: // doorframe L up
                        xyz.y += s;
                        break;
                    case 6: // doorframe top
                        xyz.x += s;
                        break;
                    case 7: // doorframe R down
                        xyz.y -= s;
                        break;
                    case 8: // strand #2 back main
                        xyz.x += s * 90 / 95;
                        break;
                    case 9:
                        xyz.x += s * 46 / 48;
                        break;
                    case 10:
                        xyz.x += s;
                        break;
                    case 11: // hanging end (always off)
                        xyz.x -= 0.8 * s;
                        xyz.y -= 0.1 * s;
                        break;
                    case 12: // downspout
                        xyz.y -= s;
                        break;
                }
            }
        }

        // 600 grid layout: 24x25 grid
        // 2 300-light strings go up and down from the center, so it's split in the middle of row 12.
        // [0..300) start at center and have decreasing Y (downward)
        // [300..600) start at center have increasing Y (upward)
        // rows alternate right-to-left, left-to-right
        // Top row:     599 598 597 ...                                 ... 576
        // Middle row:  311 310 309 ... 302 301 300   0   1   2 ...   9  10  11
        // Bottom row:  276 277 278 ...                         ... 297 298 299
        //
        // Grid is centered at (0,0) spatially. Grid spacing is 0.1 units.
        // Increasing Y goes upward, to match proprietary detected layouts.
        public static void Initialize600GridLayout(XYZ[] coordinates, int startOffset)
        {
            Debug.Assert(coordinates.Length >= startOffset + 600);

            double s = 0.1; // grid spacing
            double ctrX = 0.0, ctrY = 0.0;

            // position both strings using rotational symmetry
            for (int j = 0; j < 300; ++j)
            {
                double xoff = s * 25.0 * (-0.5 + Waveform.Triangle(j - 35.5, 48.0));
                double yoff = s * ((j + 12) / 24);
                coordinates[j + startOffset      ] = new XYZ { x = ctrX - xoff, y = ctrY - yoff };
                coordinates[j + startOffset + 300] = new XYZ { x = ctrX + xoff, y = ctrY + yoff };
            }
        }

        public struct PointI
        {
            public int x, y;
        }

        public static void Initialize600GridLayoutIndex(out PointI[] coordinates, out int[] indexGrid)
        {
            coordinates = new PointI[600];

            // index into coordinates: so 0..23 is top row, 24..47 2nd row, etc.
            indexGrid = new int[600];

            int x = 11, y = 12;
            for (int j = 0; j < 300; ++j)
            {
                coordinates[j] = new PointI { x = x, y = y };
                coordinates[j + 300] = new PointI { x = 23 - x, y = 24 - y };

                indexGrid[x + 24 * y] = j;
                indexGrid[23 - x + 24 * (24 - y)] = j + 300;

                if (j % 24 == 11)               
                    --y;                        // of 48, 11 and 35 go up
                else if ((j + 12) % 48 >= 24)
                    ++x;                        // 12..35 go right
                else
                    --x;                        // 0..10, 35..47 go left
            }
        }
    }

}
