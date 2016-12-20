using Akka.Actor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;

namespace ChartApp.Actors
{
    public class ChartingActor : ReceiveActor
    {
        #region Public Fields

        /// <summary>
        /// Maximum number of points we will allow in a series
        /// </summary>
        public const int MaxPoints = 250;

        #endregion Public Fields

        #region Private Fields

        private readonly Chart _chart;
        private Dictionary<string, Series> _seriesIndex;

        /// <summary>
        /// Incrementing counter we use to plot along the X-axis
        /// </summary>
        private int xPosCounter = 0;

        #endregion Private Fields

        #region Public Constructors

        public ChartingActor(Chart chart, Button pauseButton) : this(chart, pauseButton, new Dictionary<string, Series>())
        {
        }

        public ChartingActor(Chart chart, Button pauseButton, Dictionary<string, Series> seriesIndex)
        {
            _pauseButton = pauseButton;
            _chart = chart;
            _seriesIndex = seriesIndex;

            Charting();
        }

        #endregion Public Constructors

        #region Public Properties

        public Button PauseButton { get; set; }
        private readonly Button _pauseButton;

        #endregion Public Properties

        #region Private Methods

        private void Charting()
        {
            Receive<InitializeChart>(ic => HandleInitialize(ic));
            Receive<AddSeries>(addSeries => HandleAddSeries(addSeries));
            Receive<RemoveSeries>(removeSeries => HandleRemoveSeries(removeSeries));
            Receive<Metric>(metric => HandleMetrics(metric));

            Receive<TogglePause>(pause =>
            {
                SetPauseButtonText(true);
                BecomeStacked(Paused);
            });
        }

        private void HandleAddSeries(AddSeries series)
        {
            if (!string.IsNullOrEmpty(series.Series.Name) && !_seriesIndex.ContainsKey(series.Series.Name))
            {
                _seriesIndex.Add(series.Series.Name, series.Series);
                _chart.Series.Add(series.Series);
                SetChartBoundaries();
            }
        }

        private void HandleInitialize(InitializeChart ic)
        {
            if (ic.InitialSeries != null)
            {
                //swap the two series out
                _seriesIndex = ic.InitialSeries;
            }

            //delete any existing series
            _chart.Series.Clear();

            //set the axes up
            var area = _chart.ChartAreas[0];
            area.AxisX.IntervalType = DateTimeIntervalType.Number;
            area.AxisY.IntervalType = DateTimeIntervalType.Number;

            SetChartBoundaries();

            //attempt to render the initial chart
            if (_seriesIndex.Any())
            {
                foreach (var series in _seriesIndex)
                {
                    //force both the chart and the internal index to use the same names
                    series.Value.Name = series.Key;
                    _chart.Series.Add(series.Value);
                }
            }

            SetChartBoundaries();
        }

        private void HandleMetrics(Metric metric)
        {
            if (!string.IsNullOrEmpty(metric.Series) && _seriesIndex.ContainsKey(metric.Series))
            {
                var series = _seriesIndex[metric.Series];
                if (series.Points == null) return; // means we're shutting down
                series.Points.AddXY(xPosCounter++, metric.CounterValue);
                while (series.Points.Count > MaxPoints) series.Points.RemoveAt(0);
                SetChartBoundaries();
            }
        }

        private void HandleMetricsPaused(Metric metric)
        {
            if (string.IsNullOrEmpty(metric.Series) || !_seriesIndex.ContainsKey(metric.Series)) return;
            var series = _seriesIndex[metric.Series];
            series.Points.AddXY(xPosCounter++, 0.0d);
            while (series.Points.Count > MaxPoints) series.Points.RemoveAt(0);
            SetChartBoundaries();
        }

        private void HandleRemoveSeries(RemoveSeries series)
        {
            if (!string.IsNullOrEmpty(series.SeriesName) && _seriesIndex.ContainsKey(series.SeriesName))
            {
                var seriesToRemove = _seriesIndex[series.SeriesName];
                _seriesIndex.Remove(series.SeriesName);
                _chart.Series.Remove(seriesToRemove);
                SetChartBoundaries();
            }
        }

        private void Paused()
        {
            Receive<Metric>(metric => HandleMetricsPaused(metric));
            Receive<TogglePause>(pause =>
            {
                SetPauseButtonText(false);
                UnbecomeStacked();
            });
        }

        private void SetChartBoundaries()
        {
            double maxAxisX, maxAxisY, minAxisX, minAxisY = 0.0d;
            var allPoints = _seriesIndex.Values.SelectMany(series => series.Points).ToList();
            var yValues = allPoints.SelectMany(point => point.YValues).ToList();
            maxAxisX = xPosCounter;
            minAxisX = xPosCounter - MaxPoints;
            maxAxisY = yValues.Count > 0 ? Math.Ceiling(yValues.Max()) : 1.0d;
            minAxisY = yValues.Count > 0 ? Math.Floor(yValues.Min()) : 0.0d;

            // ReSharper disable once CompareOfFloatsByEqualityOperator
            if (minAxisY == maxAxisY)
                maxAxisY++;

            var area = _chart.ChartAreas[0];

            area.AxisY.Minimum = minAxisY;
            area.AxisY.Maximum = Math.Max(1.0d, maxAxisY);

            if (allPoints.Count > 2)
            {
                area.AxisX.Minimum = minAxisX;
                area.AxisX.Maximum = maxAxisX;
            }
        }

        private void SetPauseButtonText(bool paused)
        {
            _pauseButton.Text = string.Format("{0}", !paused ? "Pause ||" : "Resume ->");
        }

        #endregion Private Methods

        #region Public Classes

        /// <summary>
        /// Add a new <see cref="Series"/> to the chart
        /// </summary>
        public class AddSeries
        {
            #region Public Constructors

            public AddSeries(Series series)
            {
                Series = series;
            }

            #endregion Public Constructors

            #region Public Properties

            public Series Series { get; private set; }

            #endregion Public Properties
        }

        public class InitializeChart
        {
            #region Public Constructors

            public InitializeChart(Dictionary<string, Series> initialSeries)
            {
                InitialSeries = initialSeries;
            }

            #endregion Public Constructors

            #region Public Properties

            public Dictionary<string, Series> InitialSeries { get; private set; }

            #endregion Public Properties
        }

        /// <summary>
        /// Remove an existing <see cref="Series"/> from the chart
        /// </summary>
        public class RemoveSeries
        {
            #region Public Constructors

            public RemoveSeries(string seriesName)
            {
                SeriesName = seriesName;
            }

            #endregion Public Constructors

            #region Public Properties

            public string SeriesName { get; private set; }

            #endregion Public Properties
        }

        public class TogglePause
        {
        }

        #endregion Public Classes
    }
}