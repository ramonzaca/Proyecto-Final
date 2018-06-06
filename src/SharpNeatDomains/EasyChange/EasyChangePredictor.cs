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
    /// <summary>
    /// Predictor class for an EasyChange experiment
    /// </summary>
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

        public void Predict(string predictionFilePath, bool normalizeData,int normalizeRange)
        {
            double output;
            double voteYes;
            double voteNo;

            // We load the dataset
            List<double[]> valuesFROMcsv = EasyChangeDataLoader.loadDataset(_datasetPath);

            // We create the writer for the output file.
            StreamWriter writer = new StreamWriter("Predictions/" + predictionFilePath);

            // If data must be normalized
            if (normalizeData)
            {
                double[] secArray = new double[valuesFROMcsv.Count];
                for (int i = 0; i < valuesFROMcsv[0].Length; i++) 
                {
                    for (int j = 0; j < valuesFROMcsv.Count; j++)
                    {
                        secArray[j] = valuesFROMcsv[j][i];
                    }
                    var normalizedArray = EasyChangeDataLoader.NormalizeData(secArray, 0, normalizeRange);
                    for (int j = 0; j < valuesFROMcsv.Count; j++)
                    {
                        valuesFROMcsv[j][i] = normalizedArray[j];
                    }

                }
            }

            // For each case in the dataset file, all genomes vote on what the outcome should be.
            // If there is only one genome (champion case), the election is prety rigged.
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

                        // Voting part.
                        if (output >= 1)
                            voteYes += 1;
                        else
                            voteNo += 1;
                    }
                }

                // After all genomes have voted, the mayority is writen in the output file.
                if (voteYes > voteNo)
                    writer.WriteLine("1");
                else if (voteYes < voteNo)
                    writer.WriteLine("0");
                else
                    writer.WriteLine("Inconclusive");
            }
            writer.Close();
        }

    }
}
