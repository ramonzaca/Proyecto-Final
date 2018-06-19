using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using System.IO;
using System.Globalization;
using System.Diagnostics;

namespace SharpNeat.Domains.EasyChange
{
    /// <summary>
    /// DataLoader class for an EasyChange experiment.
    /// </summary>
    public class EasyChangeDataLoader
    {
        static List<double[]> _moleculeCaracteristics;
        static int _moleculesCount;
        static int _caracteristicsCount; 
        static List<double[]> _moleculesData;
        static bool[] _moleculesResults;

        #region Properties

        public List<double[]> MoleculeCaracteristics
        {
            get { return _moleculeCaracteristics; }
        }

        public int MoleculesCount
        {
            get { return _moleculesCount; }
        }

        public int CaracteristicsCount
        {
            get { return _caracteristicsCount; }
        }

        public List<double[]> MoleculesData
        {
            get { return _moleculesData; }
        }

        public bool[] MoleculesResults
        {
            get { return _moleculesResults; }
        }

        #endregion

        /// <summary>
        /// Initialization method for the dataloader.
        /// </summary>
        public void Initialize(string jsonPath = @"",bool normalizeData = true, int normalizeRange = 2, int seed = 8978  )
        {

            // Loads data from the specified file.
            _moleculeCaracteristics = loadDataset(jsonPath);

            //double[] debuj = GetRange(_moleculeCaracteristics);

            _moleculesCount = _moleculeCaracteristics.Count;
            _caracteristicsCount = _moleculeCaracteristics[0].Length - 1;
            _moleculesData = new List<double[]>();
           
           // Data is randomly shuffled form a specific seed for experiment recreation purposes.
           // Shuffle(seed);

            // Results are gathered.
            _moleculesResults = new bool[_moleculesCount];
            for (int p = 0; p < _moleculesCount; p++)
                if (_moleculeCaracteristics[p][_caracteristicsCount] == 1.0)
                    _moleculesResults[p] = true;
                else
                    _moleculesResults[p] = false;


            // If data is normalized.
            if (normalizeData)
            {
                double[] secArray = new double[_moleculeCaracteristics.Count];
                double[] normalizedArray;
                for (int i = 0; i < _moleculeCaracteristics[0].Length - 1; i++) // The result is not normalized
                {
                    for (int j = 0; j < _moleculeCaracteristics.Count; j++)
                    {
                        secArray[j] = _moleculeCaracteristics[j][i];
                    }
                    normalizedArray = NormalizeData(secArray, 0, normalizeRange);
                    for (int p = 0; p < _moleculeCaracteristics.Count; p++)
                    {
                        _moleculeCaracteristics[p][i] = normalizedArray[p];
                    }

                }
            }
            
            // Separates the data from the result
            for (int i = 0; i < _moleculeCaracteristics.Count; i++)
            {
                _moleculesData.Add(_moleculeCaracteristics[i].Take(_caracteristicsCount).ToArray());
            }

        }

        /// <summary>
        /// Loads data form the speficied file and returns it as a list of doubles.
        /// </summary>
        public static List<double[]> loadDataset(string jsonPath = @"")
        {
            List<double[]> valuesFROMcsv = new List<double[]>();

            // We open the file and read line by line, converting each number into a double type and adding it to the 
            // exit list of arrays.
            StreamReader reader = File.OpenText(jsonPath);
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                string[] items = line.Split(',');

                double[] itemsAsDouble = new double[items.Length];
                // Numbers are converted.
                for (int i = 0; i < items.Length; i++)
                {
                    if (items[i].Count(f => f == '.') > 1)
                        itemsAsDouble[i] = Double.Parse(items[i]) / 1000;
                    else
                        itemsAsDouble[i] = Double.Parse(items[i], CultureInfo.InvariantCulture);
                    Debug.Assert(!Double.IsNaN(itemsAsDouble[i]));
                }
                valuesFROMcsv.Add(itemsAsDouble);
            }                    
            return valuesFROMcsv;
        }

        /// <summary>
        /// Method to make lineal normalization of data.
        /// </summary>
        public static double[] NormalizeData(IEnumerable<double> data, int min, int max)
        {
            double dataMax = data.Max();
            double dataMin = data.Min();
            double range = dataMax - dataMin;
            if (range != 0)
                return data
                    .Select(d => (d - dataMin) / range)
                    .Select(n => ((1 - n) * min + n * max))
                    .ToArray();
            else
                return new double[data.Count()];
        }

        /// <summary>
        /// Method to check the range in each caracterist of all molecules of the dataset. Only used in debug purposes.
        /// </summary>
        private double[] GetRange(List<double[]> valuesFROMcsv)
        {
            double[] range = new double[valuesFROMcsv[0].Length];
            double min = 0;
            double max = 0;
            for (int i = 0; i < valuesFROMcsv[0].Length; i++)
                for (int j = 0; j < valuesFROMcsv.Count; j++)
                {
                    if (j == 0)
                    {
                        min = valuesFROMcsv[j][i];
                        max = valuesFROMcsv[j][i];
                    }
                    else
                    {
                        if (valuesFROMcsv[j][i] < min)
                            min = valuesFROMcsv[j][i];
                        else if (valuesFROMcsv[j][i] > max)
                            max = valuesFROMcsv[j][i];
                    }
                    range[i] = max - min;
                }
            return range;
        }

        /// <summary>
        /// Randomly shuffles the stored data from a specified seed for experiment reacreation purposes.
        /// </summary>
        private static void Shuffle(int seed)
        {

            Random rng = new Random(seed);
            int n = _moleculeCaracteristics.Count;
            while (n > 1)
            {
                n--;
                int k = rng.Next(n + 1);
                var value = _moleculeCaracteristics[k];
                _moleculeCaracteristics[k] = _moleculeCaracteristics[n];
                _moleculeCaracteristics[n] = value;
            }
        }
    }
}
