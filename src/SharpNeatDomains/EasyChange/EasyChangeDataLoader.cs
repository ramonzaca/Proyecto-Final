using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using System.IO;
using System.Globalization;

namespace SharpNeat.Domains.EasyChange
{
    public class EasyChangeDataLoader
    {
        static List<double[]> moleculeCaracteristics;
        static int _totalImageCount;
        static int _pixelCount; // Todos las moléculas tienen la misma cantidad de características
        static double[] allIdentifiers;
        static List<double[]> allImages;
        static bool[] testClases;

        #region Properties

        public List<double[]> MoleculeCaracteristics
        {
            get
            {
                return moleculeCaracteristics;
            }
        }

        public int TotalImageCount
        {
            get
            {
                return _totalImageCount;
            }
        }

        public int PixelCount
        {
            get
            {
                return _pixelCount;
            }
        }

        public double[] AllIdentifiers
        {
            get
            {
                return allIdentifiers;
            }
        }

        public List<double[]> AllImages
        {
            get
            {
                return allImages;
            }
        }

        public bool[] TestClases
        {
            get
            {
                return testClases;
            }
        }

   

        #endregion

        // Función de inicialización de cargado de datos
        public void Initialize(string jsonPath = @"",bool normalizeData = true, int normalizeRange = 2, int seed = 8978  )
        {

            moleculeCaracteristics = loadDataset(jsonPath);
            //double[] debuj = GetRange(moleculeCaracteristics);
            _totalImageCount = moleculeCaracteristics.Count;
            _pixelCount = moleculeCaracteristics[0].Length - 1;
            allIdentifiers = new double[_totalImageCount];
            allImages = new List<double[]>();
           
            

            // Mezclo los resultados
            Shuffle(seed);

            // Si se decide normalizar los datos
            if (normalizeData)
            {
                double[] secArray = new double[moleculeCaracteristics.Count];
                for (int i = 0; i < moleculeCaracteristics[0].Length - 1; i++) // No normalizar la salida
                {
                    for (int j = 0; j < moleculeCaracteristics.Count; j++)
                    {
                        secArray[j] = moleculeCaracteristics[j][i];
                    }
                    var normalizedArray = NormalizeData(secArray, -normalizeRange, normalizeRange);
                    for (int j = 0; j < moleculeCaracteristics.Count; j++)
                    {
                        moleculeCaracteristics[j][i] = normalizedArray[j];
                    }

                }
            }
            // Selecciono la salida
            for (int i = 0; i < moleculeCaracteristics.Count; i++)
            {
                // El valor de la propiedad a calcular se encuentra al final del arreglo
                allIdentifiers[i] = moleculeCaracteristics[i][_pixelCount];

                // Se evalúan todas las propiedades
                allImages.Add(moleculeCaracteristics[i].Take(_pixelCount).ToArray());
            }

            //Para analisis de clases

            testClases = new bool[allIdentifiers.Length];
            for (int p = 0; p < allIdentifiers.Length; p++)
                if (allIdentifiers[p] == 1.0)
                    testClases[p] = true;
                else
                    testClases[p] = false;
        }

        // Obtiene el la información del dataset y lo devuleve como una lista de doubles
        public static List<double[]> loadDataset(string jsonPath = @"")
        {
            List<double[]> valuesFROMcsv = new List<double[]>();
            int count = jsonPath.Split('\\').Length;
            string filesPath;
            if (jsonPath.Split('\\').Length > 1) {
                filesPath = jsonPath;
            }
            else { 
            
                string currentAssemblyDirectoryName = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                filesPath = currentAssemblyDirectoryName + "/../../../Data/" + jsonPath;
            }
            int lineCount = 0;

            StreamReader reader = File.OpenText(filesPath);
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                
                string[] items = line.Split(',');

                if (lineCount > 0)
                {

                    double[] itemsAsDouble = new double[items.Length - 1 ];
                    // Se convierten los valores uno a uno para evitar problemas en el modo en que estan guardados los datos 
                    for (int i = 0; i < items.Length - 1 ; i++)
                    {
                        if (items[i].Count(f => f == '.') > 1)
                            itemsAsDouble[i] = Double.Parse(items[i]) / 1000;
                        else if (items[i].SequenceEqual("RB"))
                            itemsAsDouble[i] = 1.0;
                        else if (items[i].SequenceEqual("NRB"))
                            itemsAsDouble[i] = 0.0;
                        else
                            itemsAsDouble[i] = Double.Parse(items[i], CultureInfo.InvariantCulture);
                    }
                    valuesFROMcsv.Add(itemsAsDouble);
                }
                lineCount++;
            }
                    
            return valuesFROMcsv;
        }

        //Función secundaria de normalizado
        private static double[] NormalizeData(IEnumerable<double> data, int min, int max)
        {
            double dataMax = data.Max();
            double dataMin = data.Min();
            double range = dataMax - dataMin;

            return data
                .Select(d => (d - dataMin) / range)
                .Select(n => ((1 - n) * min + n * max))
                .ToArray();
        }

        // Devuelve un arreglo con el rango de cada caracteristica de las moleculas. Solo para propositos de debuj
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

        public static void Shuffle(int seed)
        {

            Random rng = new Random(seed);
            int n = moleculeCaracteristics.Count;
            while (n > 1)
            {
                n--;
                int k = rng.Next(n + 1);
                var value = moleculeCaracteristics[k];
                moleculeCaracteristics[k] = moleculeCaracteristics[n];
                moleculeCaracteristics[n] = value;
            }
        }

    }
}
