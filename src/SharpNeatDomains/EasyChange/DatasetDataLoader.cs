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
    public class DatasetDataLoader
    {
      
        // Obtiene el la información del dataset y lo devuleve como una lista de doubles
        public static List<double[]> loadDataset(string jsonPath = @"")
        {

            List<double[]> valuesFROMcsv = new List<double[]>();
            string currentAssemblyDirectoryName = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string filesPath = currentAssemblyDirectoryName + "/../../../Data/" + EasyChangeParams.FILENAME;

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


    }
}
