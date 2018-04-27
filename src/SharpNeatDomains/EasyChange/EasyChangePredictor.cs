using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using SharpNeat.Core;
using SharpNeat.Genomes.Neat;
using SharpNeat.Phenomes;

namespace SharpNeat.Domains.EasyChange
{
    public class EasyChangePredictor
    {
        string _genomePath;
        string _datasetPath;
        string _predictionPath;
        EasyChangeExperiment _experiment;
        bool _statusCompleted;
        List<NeatGenome> _genomeList;

        public EasyChangePredictor(EasyChangeExperiment experiment) {
            _experiment = experiment;
            _statusCompleted = false;
        }

        public bool StatusCompleted
        {
            get { return _statusCompleted; }
            set { _statusCompleted = value; }
        }

        public string GenomePath
        {
            get { return _genomePath; }
            set { _genomePath = value; }
        }

        public string DatasetPath
        {
            get { return _datasetPath; }
            set { _datasetPath = value; }
        }

        public string PredictionPath
        {
            get { return _predictionPath; }
            set { _predictionPath = value; }
        }

        public List<NeatGenome> GenomeList
        {
            get { return _genomeList; }
        }

        public void loadPopulation()
        {
            using (XmlReader xr = XmlReader.Create(_genomePath))
            {
                _genomeList = _experiment.LoadPopulation(xr);
            }
        }

        public int Predict(string predictionFilePath, bool normalizeData,int normalizeRange)
        {
            double output;
            double voteYes;
            double voteNo;

            List<double[]> valuesFROMcsv = EasyChangeDataLoader.loadDataset(_datasetPath);

            StreamWriter writer = new StreamWriter("Predicciones/" + predictionFilePath);

            // Normalizado de la informacion
            if (normalizeData)
            {
                double[] secArray = new double[valuesFROMcsv.Count];
                for (int i = 0; i < valuesFROMcsv[0].Length; i++) // No normalizar la salida
                {
                    for (int j = 0; j < valuesFROMcsv.Count; j++)
                    {
                        secArray[j] = valuesFROMcsv[j][i];
                    }
                    var normalizedArray = EasyChangeDataLoader.NormalizeData(secArray, -normalizeRange, normalizeRange);
                    for (int j = 0; j < valuesFROMcsv.Count; j++)
                    {
                        valuesFROMcsv[j][i] = normalizedArray[j];
                    }

                }
            }

            foreach (double[] inputs in valuesFROMcsv)
            {
                voteNo = 0;
                voteYes = 0;
                for (int i = 0; i < _genomeList.Count; i++)
                {
                    IBlackBox box;
                    if (null == _genomeList[i].CachedPhenome)
                        _genomeList[i].CachedPhenome = _experiment.CreateGenomeDecoder().Decode(_genomeList[i]);
                    box = (IBlackBox)_genomeList[i].CachedPhenome;
                    ISignalArray inputArr = box.InputSignalArray;
                    ISignalArray outputArr = box.OutputSignalArray;

                    for (int j = 0; j < inputs.Length; j++)
                    {
                        inputArr[j] = inputs[j];
                    }

                    // Activate the black box.
                    box.Activate();
                    if (box.IsStateValid)
                    {   

                        // Read output signal.
                        output = outputArr[0];

                        // Reset black box state ready for next test case.
                        box.ResetState();


                        if (output >= 0.5)
                            voteYes += 1;
                        else
                            voteNo += 1;
                    }
                }
                if (voteYes > voteNo)
                    writer.WriteLine("1");
                else if (voteYes < voteNo)
                    writer.WriteLine("0");
                else
                    writer.WriteLine("Inconclusive");
            }
            writer.Close();
            return 1;
        }

    }
}
