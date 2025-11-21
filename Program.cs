#nullable disable //Disables strict warnings about null values when compiling.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO; //For File.Exists and File.ReadAllLines, when reading the csv.
using System.Linq;

namespace CSharp_F1_Alpine_Lap_Time_Analyzer
{
    class Program
    {
        static void Main(string[] args)
        {
            //Ensure nice output formatting for symbols.
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            Console.WriteLine("Alpine Lap Time Analyzer");
            Console.WriteLine("========================");
            
            string csvPath = "race.csv";

            //Use the "Worker" method to load the data.
            List<RaceLap> data = ReadRaceData(csvPath);

            //If we got data, use the "Analyzer" method to show stats.
            if (data.Count > 0)
            {
                Console.WriteLine($"File: {csvPath}");
                Console.WriteLine($"Laps loaded: {data.Count} - Sao Paulo Grand Prix 2025, Brazil");
                Console.WriteLine();

                AnalyzeRace(data);
            }
            else
            {
                Console.WriteLine("No valid data found to analyze.");
            }

        }

        //This method is ONLY responsible for reading the file and creating the list.
        static List<RaceLap> ReadRaceData(string path)
        {
            //Prepare a list to hold our converted data objects.
            List<RaceLap> raceLaps = new List<RaceLap>();

            //Check if file exists.
            if (!File.Exists(path))
            {
                Console.WriteLine($"Error: The file '{path}' was not found.");
                return raceLaps; //Return the empty list.
            }

            //Read all lines into an array of strings.
            string[] lines = File.ReadAllLines(path);

            //Loop through every single line in the file.
            foreach (string line in lines)
            {
                //Skip empty lines to be safe.
                if (string.IsNullOrWhiteSpace(line)) continue;

                //Filter out headers.
                //If the first character is NOT a digit, it is a header or metadata, so we skip it.
                if (!char.IsDigit(line[0])) continue;

                //Split the row by commas.
                string[] parts = line.Split(',');

                RaceLap lap = new RaceLap();

                //Parse the Lap Number.
                lap.LapNumber = int.Parse(parts[0]);

                //Parse Gasly Data.
                lap.Gas.Time = ParseDoubleOrNull(parts[1]);
                lap.Gas.Position = ParseIntOrNull(parts[3]);
                lap.Gas.TyreCompound = parts[5];
                lap.Gas.Pitstop = ParseBool(parts[7]);
                lap.Gas.Status = ParseIntOrNull(parts[13]);

                //Parse Colapinto Data.
                lap.Col.Time = ParseDoubleOrNull(parts[15]);
                lap.Col.Position = ParseIntOrNull(parts[17]);
                lap.Col.TyreCompound = parts[19];
                lap.Col.Pitstop = ParseBool(parts[21]);
                lap.Col.Status = ParseIntOrNull(parts[27]);

                raceLaps.Add(lap);
            }

            return raceLaps;
        }

        //This method receives the raw data and does the math/printing.
        static void AnalyzeRace(List<RaceLap> raceLaps)
        {
            int gasStops = 0;
            int colStops = 0;

            double gasFastest = double.MaxValue;
            double colFastest = double.MaxValue;

            //Lists to store clean laps for Average and Median calculations.
            List<double> gasCleanLapsList = new List<double>();
            List<double> colCleanLapsList = new List<double>();

            //Store Tyre Compund user order.
            List<string> gasCompounds = new List<string>();
            List<string> colCompounds = new List<string>();

            //To calculate the pace delta on overlapping laps.
            List<double> overlappingDeltas = new List<double>();

            foreach (RaceLap lap in raceLaps)
            {
                //GASLY ANALYSIS
                if (lap.Gas.Pitstop) gasStops++; //Counting Pitstops.

                //Collect Tyre Compound.
                string gTyre = lap.Gas.TyreCompound;
                if (!string.IsNullOrEmpty(gTyre))
                {
                    //If we have no tyres yet, or the current tyre is different from the last one we added.
                    if (gasCompounds.Count == 0 || gasCompounds[gasCompounds.Count - 1] != gTyre)
                    {
                        gasCompounds.Add(gTyre);
                    }
                }

                if (lap.Gas.Time.HasValue)
                {
                    //Fastest Lap Check.
                    if (lap.Gas.Time.Value < gasFastest)
                        gasFastest = lap.Gas.Time.Value;

                    //Clean Lap Check for Stats (Status 1 = Green Flag, No Pitstop).
                    if (lap.Gas.Status == 1 && !lap.Gas.Pitstop)
                    {
                        gasCleanLapsList.Add(lap.Gas.Time.Value);
                    }
                }

                //COLAPINTO ANALYSIS
                if (lap.Col.Pitstop) colStops++; //Counting Pitstops.

                //Collect Tyre Compound.
                string cTyre = lap.Col.TyreCompound;
                if (!string.IsNullOrEmpty(cTyre))
                {
                    //If we have no tyres yet, or the current tyre is different from the last one we added.
                    if (colCompounds.Count == 0 || colCompounds[colCompounds.Count - 1] != cTyre)
                    {
                        colCompounds.Add(cTyre);
                    }
                }

                if (lap.Col.Time.HasValue)
                {
                    //Fastest Lap Check.
                    if (lap.Col.Time.Value < colFastest)
                        colFastest = lap.Col.Time.Value;

                    //Clean Lap Check for Stats.
                    if (lap.Col.Status == 1 && !lap.Col.Pitstop)
                    {
                        colCleanLapsList.Add(lap.Col.Time.Value);
                    }
                }

                //Collect Delta if both are Green Flag.
                if (lap.Gas.Status == 1 && lap.Col.Status == 1 && lap.Gas.Time.HasValue && lap.Col.Time.HasValue)
                {
                    overlappingDeltas.Add(lap.Gas.Time.Value - lap.Col.Time.Value);
                }
            }

            //CALCULATIONS

            //Averages.
            double gasTotalTime = 0;
            foreach (var t in gasCleanLapsList) gasTotalTime += t;
            double gasAvg = gasCleanLapsList.Count > 0 ? gasTotalTime / gasCleanLapsList.Count : 0;

            double colTotalTime = 0;
            foreach (var t in colCleanLapsList) colTotalTime += t;
            double colAvg = colCleanLapsList.Count > 0 ? colTotalTime / colCleanLapsList.Count : 0;

            //Medians.
            double gasMedian = CalculateMedian(gasCleanLapsList);
            double colMedian = CalculateMedian(colCleanLapsList);

            //Summary Table.
            
            string fmt = "{0,-15} | {1,-15} | {2,-15}"; //Formatting alignment from file.

            Console.WriteLine(String.Format(fmt, "METRIC", "GASLY", "COLAPINTO"));
            Console.WriteLine(new string('-', 54));

            //Helper to print rows with conditional coloring.
            PrintMetricRow("Fastest Lap", gasFastest == double.MaxValue ? 0 : gasFastest, colFastest == double.MaxValue ? 0 : colFastest);
            PrintMetricRow("Avg Pace", gasAvg, colAvg);
            PrintMetricRow("Median Pace", gasMedian, colMedian);

            //Pitstops (Simple white text).
            Console.WriteLine(String.Format(fmt, "Pitstops", gasStops, colStops));

            Console.Write("Tyres".PadRight(16) + "| ");
            int lenGas = PrintTyreList(gasCompounds); 
            int padGas = 16 - lenGas;
            if (padGas > 0) Console.Write(new string(' ', padGas));

            Console.Write("| ");
            PrintTyreList(colCompounds.ToList());
            Console.WriteLine();

            Console.WriteLine(new string('-', 54));

            //Pace Delta Footer.
            if (overlappingDeltas.Any())
            {
                double avgDelta = overlappingDeltas.Average();
                string winner = avgDelta < 0 ? "GASLY" : "COLAPINTO"; 

                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write($"PACE DELTA: {winner} was faster by {Math.Abs(avgDelta):F3}s per lap (green flags no pitstops).");
                Console.ResetColor();
                Console.WriteLine();
                Console.WriteLine(new string('=', 74));
            }

            //Detailed Table all laps.

            Console.WriteLine();
            Console.WriteLine("All race laps (where both drivers have a time):");

            //Define precise widths from file code.
            int wLap = 3;
            int wTime = 11;
            int wDelta = 17;
            int wPos = 8;
            int wStat = 11;
            int wPit = 8;

            //Header Row.
            Console.Write("Lap".PadLeft(wLap) + " | ");
            Console.Write("GAS Lap (s)".PadLeft(wTime) + " | ");
            Console.Write("COL Lap (s)".PadLeft(wTime) + " | ");
            Console.Write("Delta (GAS - COL)".PadLeft(wDelta) + " | ");
            Console.Write("GAS Pos".PadLeft(wPos) + " | ");
            Console.Write("COL Pos".PadLeft(wPos) + " | ");
            Console.Write("GAS Status".PadLeft(wStat) + " | ");
            Console.Write("COL Status".PadLeft(wStat) + " | ");
            Console.Write("GAS Pit".PadLeft(wPit) + " | ");
            Console.Write("COL Pit".PadLeft(wPit));
            Console.WriteLine();

            //Separator Line.
            Console.WriteLine(
                new string('-', wLap) + "-+-" +
                new string('-', wTime) + "-+-" +
                new string('-', wTime) + "-+-" +
                new string('-', wDelta) + "-+-" +
                new string('-', wPos) + "-+-" +
                new string('-', wPos) + "-+-" +
                new string('-', wStat) + "-+-" +
                new string('-', wStat) + "-+-" +
                new string('-', wPit) + "-+-" +
                new string('-', wPit)
            );

            foreach (RaceLap lap in raceLaps)
            {
                //Show row if both have times.
                if (lap.Gas.Time.HasValue && lap.Col.Time.HasValue)
                {
                    double delta = lap.Gas.Time.Value - lap.Col.Time.Value;

                    Console.Write(lap.LapNumber.ToString().PadLeft(wLap) + " | ");
                    Console.Write(lap.Gas.Time.Value.ToString("F3").PadLeft(wTime) + " | ");
                    Console.Write(lap.Col.Time.Value.ToString("F3").PadLeft(wTime) + " | ");

                    //Colored Delta.
                    var oldColor = Console.ForegroundColor;
                    string txt = (delta >= 0 ? "+" : "-") + Math.Abs(delta).ToString("F3") + " s";
                    txt = txt.PadLeft(wDelta);
                    if (delta > 0) Console.ForegroundColor = ConsoleColor.Green;
                    else if (delta < 0) Console.ForegroundColor = ConsoleColor.Red;
                    Console.Write(txt);
                    Console.ForegroundColor = oldColor;
                    
                    Console.Write(" | ");

                    //Position might be null.
                    string gPos = lap.Gas.Position.HasValue ? lap.Gas.Position.ToString() : "--";
                    string cPos = lap.Col.Position.HasValue ? lap.Col.Position.ToString() : "--";
                    Console.Write(gPos.PadLeft(wPos) + " | ");
                    Console.Write(cPos.PadLeft(wPos) + " | ");

                    //Status short text.
                    Console.Write(GetShortStatus(lap.Gas.Status).PadLeft(wStat) + " | ");
                    Console.Write(GetShortStatus(lap.Col.Status).PadLeft(wStat) + " | ");

                    //Pitstop.
                    string gPit = lap.Gas.Pitstop ? "YES" : "";
                    string cPit = lap.Col.Pitstop ? "YES" : "";
                    Console.Write(gPit.PadLeft(wPit) + " | ");
                    Console.Write(cPit.PadLeft(wPit));

                    Console.WriteLine();
                }
            }
        }

        //Helper to calculate Median from a list of doubles.
        static double CalculateMedian(List<double> values)
        {
            if (values.Count == 0) return 0;
            values.Sort();

            int count = values.Count;
            if (count % 2 == 0)
            {
                //Even number of items, use average of the two middle elements.
                double a = values[count / 2 - 1];
                double b = values[count / 2];
                return (a + b) / 2.0;
            }
            else
            {
                //Odd number of items, use middle.
                return values[count / 2];
            }
        }

        //Helper to print rows similar to the File format.
        static void PrintMetricRow(string label, double val1, double val2)
        {
            Console.Write(label.PadRight(16) + "| ");

            //Gasly
            if (val1 < val2 && val1 > 0) Console.ForegroundColor = ConsoleColor.Green;
            Console.Write($"{val1:F3}s".PadRight(16));
            Console.ResetColor();

            Console.Write("| ");

            //Colapinto
            if (val2 < val1 && val2 > 0) Console.ForegroundColor = ConsoleColor.Green;
            Console.Write($"{val2:F3}s");
            Console.ResetColor();

            Console.WriteLine();
        }

        //Helper to print Tyre Compounds with colors (File methodology).
        static int PrintTyreList(List<string> compounds)
        {
            int printedLength = 0;
            for (int i = 0; i < compounds.Count; i++)
            {
                string c = compounds[i];
                var old = Console.ForegroundColor;
                string upper = c.Trim().ToUpper();
                
                if (upper == "SOFT") Console.ForegroundColor = ConsoleColor.Red;
                else if (upper == "MEDIUM") Console.ForegroundColor = ConsoleColor.Yellow;
                else if (upper == "HARD") Console.ForegroundColor = ConsoleColor.White;
                
                Console.Write(c);
                Console.ForegroundColor = old;
                
                printedLength += c.Length;

                if (i < compounds.Count - 1)
                {
                    Console.Write(", ");
                    printedLength += 2;
                }
            }
            return printedLength;
        }

        //Codes detailed in the csv.
        static string GetShortStatus(int? status)
        {
            if (status == 1) return "Green";
            if (status == 2) return "Yellow";
            if (status == 4) return "SC";
            if (status == 5) return "Red";
            if (status == 6) return "VSC";
            if (status == 7) return "VSC end";
            return "";
        }

        //Helper to safely parse a double (returns null if empty).
        static double? ParseDoubleOrNull(string val)
        {
            if (string.IsNullOrEmpty(val)) return null;
            return double.Parse(val, CultureInfo.InvariantCulture);
        }

        //Helper to safely parse an int (returns null if empty).
        static int? ParseIntOrNull(string val)
        {
            if (string.IsNullOrEmpty(val)) return null;
            return int.Parse(val);
        }

        //Helper to parse booleans from "TRUE"/"FALSE" text.
        static bool ParseBool(string val)
        {
            if (string.IsNullOrEmpty(val)) return false;
            return val.Equals("TRUE", StringComparison.OrdinalIgnoreCase);
        }
    }

    public class LapData
    {
        public double? Time { get; set; }
        public int? Status { get; set; }
        public bool Pitstop { get; set; }
        public string TyreCompound { get; set; }
        public int? Position { get; set; }
    }

    public class RaceLap
    {
        public int LapNumber { get; set; }
        public LapData Gas { get; set; }
        public LapData Col { get; set; }

        public RaceLap()
        {
            Gas = new LapData();
            Col = new LapData();
        }
    }
}