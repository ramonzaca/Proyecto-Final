using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SharpNeat.Decoders;

namespace SharpNeat.Domains.EasyChange
{

    public class EasyChangeParams
    {

        public static string FILENAME = "dataset Todeschini - RB.csv"; // Nombre del archivo a analizar. Recodar que los dataset deben estar en la carpeta "Data"
        public static int INPUTS = 41; // Número de entradas para el NEAT. Debe ser acorde al dataset.
        public static int MAXGENERATIONS = 100000; // Cantidad de generaciones antes de comenzar la fase de testing
        public static int SAVEPERIOD = 1000; // Periodo entre el cual se guarda la población
        public static double TESTPORCENTAGE = 0.1; // Porcentage del total de los datos dedicado a testing.
        public static bool NORMALIZEDATA = true; // Se normalizan los datos
        public static int NORMALIZERANGE = 1; // Si se normalizan los datos, estos estarán en el rango [-NORMALIZERANGE,NORMALIZERANGE]
        public static int THREADS = 7; // De ejecución paralela
        public static int SEED = 8787; // Con el propósito de poder recrear experimentos, se establece un seed.


        
    }
}