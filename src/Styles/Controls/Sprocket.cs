﻿using System;
using System.Collections.Generic;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Styles.Controls
{
    internal class SprocketControl : Control, IDisposable
    {
        #region Constants

        private const double DefaultInterval = 60;
        private static readonly Color DefaultTickColor = Color.FromArgb(255, 58, 58, 58);
        private const double DefaultTickWidth = 3;
        private const int DefaultTickCount = 12;
        //private readonly Size MINIMUM_CONTROL_SIZE = new Size(2, 2);
        /*
                private const double MinimumPenWidth = 2;
        */
        private const double DefaultStartAngle = 270;
        private const double MinimumInnerRadiusFactor = 0.175;
        private const double MinimumOuterRadiusFactor = 0.3125;
        // The Lower limit of the Alpha value (The spokes will be shown in 
        // alpha values ranging from 255 to m_AlphaLowerLimit)
        private const Int32 AlphaUpperLimit = 250;
        private const Int32 AlphaLowerLimit = 0;
        private const double AlphaTickPercentageLowerLimit = 10;
        private const double DefaultProgressAlpha = 10;
        private const double DefaultProgress = 0.0;
        #endregion

        #region Enums

        /// <summary>
        /// Defines the Direction of Rotation
        /// </summary>
        public enum Direction
        {
            Clockwise,
            Anticlockwise
        }

        #endregion

        #region Structs

        /// <summary>
        /// Stores the details of each spoke
        /// </summary>
        private struct Spoke
        {
            public readonly Point StartPoint;
            public readonly Point EndPoint;

            public Spoke(Point pt1, Point pt2)
            {
                StartPoint = pt1;
                EndPoint = pt2;
            }
        }

        #endregion

        #region Fields

        private Point centerPoint;
        private double innerRadius;
        private double outerRadius;
        private double alphaChange;
        private double angleIncrement;
        private double renderStartAngle;
        private readonly Timer renderTimer;
        private List<Spoke> spokes;

        #endregion

        #region Dependency Properties

        #region AlphaTicksPercentage

        /// <summary>
        /// AlphaTicksPercentage Dependency Property
        /// </summary>
        public static readonly DependencyProperty AlphaTicksPercentageProperty =
            DependencyProperty.Register("AlphaTicksPercentage", typeof(double), typeof(SprocketControl),
                new FrameworkPropertyMetadata(100.0, OnAlphaTicksPercentageChanged, CoerceAlphaTicksPercentage));

        /// <summary>
        /// Gets or sets the AlphaTicksPercentage property. This dependency property 
        /// indicates the percentage of total ticks which must be considered for step by step reduction
        /// of the alpha value. The remaining ticks remain at the LowestAlpha value.
        /// </summary>
        public double AlphaTicksPercentage
        {
            get => (double)GetValue(AlphaTicksPercentageProperty);
            set => SetValue(AlphaTicksPercentageProperty, value);
        }

        /// <summary>
        /// Handles changes to the AlphaTicksPercentage property.
        /// </summary>
        private static void OnAlphaTicksPercentageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var sprocket = (SprocketControl)d;
            var oldAlphaTicksPercentage = (double)e.OldValue;
            var newAlphaTicksPercentage = sprocket.AlphaTicksPercentage;
            sprocket.OnAlphaTicksPercentageChanged(oldAlphaTicksPercentage, newAlphaTicksPercentage);
        }

        /// <summary>
        /// Provides derived classes an opportunity to handle changes to the AlphaTicksPercentage property.
        /// </summary>
        protected void OnAlphaTicksPercentageChanged(double oldAlphaTicksPercentage, double newAlphaTicksPercentage)
        {
        }

        /// <summary>
        /// Coerces the AlphaTicksPercentage value.
        /// </summary>
        private static object CoerceAlphaTicksPercentage(DependencyObject d, object value)
        {
            var desiredAlphaTicksPercentage = (double)value;

            if (desiredAlphaTicksPercentage > 100.0)
                desiredAlphaTicksPercentage = 100.0;
            else if (desiredAlphaTicksPercentage < AlphaTickPercentageLowerLimit)
                desiredAlphaTicksPercentage = AlphaTickPercentageLowerLimit;

            return desiredAlphaTicksPercentage;
        }

        #endregion

        #region Interval

        /// <summary>
        /// Interval Dependency Property
        /// </summary>
        public static readonly DependencyProperty IntervalProperty =
            DependencyProperty.Register("Interval", typeof(double), typeof(SprocketControl),
                new FrameworkPropertyMetadata(DefaultInterval,
                                              OnIntervalChanged));

        /// <summary>
        /// Gets or sets the Interval property. This dependency property 
        /// indicates duration at which the timer for rotation should fire.
        /// </summary>
        public double Interval
        {
            get => (double)GetValue(IntervalProperty);
            set => SetValue(IntervalProperty, value);
        }

        /// <summary>
        /// Handles changes to the Interval property.
        /// </summary>
        /// <param name="d">SprocketControl</param>
        /// <param name="e">DependencyProperty changed event arguments</param>
        private static void OnIntervalChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var sprocket = (SprocketControl)d;
            var oldInterval = (double)e.OldValue;
            var newInterval = sprocket.Interval;
            sprocket.OnIntervalChanged(oldInterval, newInterval);
        }

        /// <summary>
        /// Provides derived classes an opportunity to handle changes to the Interval property.
        /// </summary>
        /// <param name="oldInterval">Old Value</param>
        /// <param name="newInterval">New Value</param>
        protected void OnIntervalChanged(double oldInterval, double newInterval)
        {
            if (renderTimer == null)
                return;
            var isEnabled = renderTimer.Enabled;
            renderTimer.Enabled = false;
            renderTimer.Interval = newInterval;
            renderTimer.Enabled = isEnabled;
        }

        #endregion

        #region IsIndeterminate

        /// <summary>
        /// IsIndeterminate Dependency Property
        /// </summary>
        public static readonly DependencyProperty IsIndeterminateProperty =
            DependencyProperty.Register("IsIndeterminate", typeof(bool), typeof(SprocketControl),
                new FrameworkPropertyMetadata(true, OnIsIndeterminateChanged));

        /// <summary>
        /// Gets or sets the IsIndeterminate property. This dependency property 
        /// indicates whether the SprocketControl's progress is indeterminate or not.
        /// </summary>
        public bool IsIndeterminate
        {
            get => (bool)GetValue(IsIndeterminateProperty);
            set => SetValue(IsIndeterminateProperty, value);
        }

        /// <summary>
        /// Handles changes to the IsIndeterminate property.
        /// </summary>
        /// <param name="d">SprocketControl</param>
        /// <param name="e">DependencyProperty changed event arguments</param>
        private static void OnIsIndeterminateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var target = (SprocketControl)d;
            var oldIsIndeterminate = (bool)e.OldValue;
            var newIsIndeterminate = target.IsIndeterminate;
            target.OnIsIndeterminateChanged(oldIsIndeterminate, newIsIndeterminate);
        }

        /// <summary>
        /// Provides derived classes an opportunity to handle changes to the IsIndeterminate property.
        /// </summary>
        /// <param name="oldIsIndeterminate">Old Value</param>
        /// <param name="newIsIndeterminate">New Value</param>
        protected void OnIsIndeterminateChanged(bool oldIsIndeterminate, bool newIsIndeterminate)
        {
            if (oldIsIndeterminate == newIsIndeterminate)
                return;

            if ((newIsIndeterminate) && (IsVisible))
            {
                // Start the renderTimer
                Start();
            }
            else
            {
                // Stop the renderTimer
                Stop();
                InvalidateVisual();
            }
        }

        #endregion

        #region InnerRadius

        /// <summary>
        /// InnerRadius Dependency Property
        /// </summary>
        public static readonly DependencyProperty InnerRadiusProperty =
            DependencyProperty.Register("InnerRadius", typeof(double), typeof(SprocketControl),
                new FrameworkPropertyMetadata(MinimumInnerRadiusFactor, OnInnerRadiusChanged, CoerceInnerRadius));

        /// <summary>
        /// Gets or sets the InnerRadius property. This dependency property 
        /// indicates the ratio of the Inner Radius to the Width of the SprocketControl.
        /// </summary>
        public double InnerRadius
        {
            get => (double)GetValue(InnerRadiusProperty);
            set => SetValue(InnerRadiusProperty, value);
        }

        /// <summary>
        /// Handles changes to the InnerRadius property.
        /// </summary>
        private static void OnInnerRadiusChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var sprocket = (SprocketControl)d;
            var oldInnerRadius = (double)e.OldValue;
            var newInnerRadius = sprocket.InnerRadius;
            sprocket.OnInnerRadiusChanged(oldInnerRadius, newInnerRadius);
        }

        /// <summary>
        /// Provides derived classes an opportunity to handle changes to the InnerRadius property.
        /// </summary>
        protected void OnInnerRadiusChanged(double oldInnerRadius, double newInnerRadius)
        {
            // Recalculate the spoke points
            CalculateSpokesPoints();
        }

        /// <summary>
        /// Coerces the InnerRadius value.
        /// </summary>
        private static object CoerceInnerRadius(DependencyObject d, object value)
        {
            var desiredInnerRadius = (double)value;

            return desiredInnerRadius;
        }

        #endregion

        #region LowestAlpha

        /// <summary>
        /// LowestAlpha Dependency Property
        /// </summary>
        public static readonly DependencyProperty LowestAlphaProperty =
            DependencyProperty.Register("LowestAlpha", typeof(Int32), typeof(SprocketControl),
                new FrameworkPropertyMetadata(AlphaLowerLimit, OnLowestAlphaChanged, CoerceLowestAlpha));

        /// <summary>
        /// Gets or sets the LowestAlpha property. This dependency property 
        /// indicates the lowest Opacity value that must be used while rendering the SprocketControl's spokes.
        /// </summary>
        public Int32 LowestAlpha
        {
            get => (Int32)GetValue(LowestAlphaProperty);
            set => SetValue(LowestAlphaProperty, value);
        }

        /// <summary>
        /// Handles changes to the LowestAlpha property.
        /// </summary>
        private static void OnLowestAlphaChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var sprocket = (SprocketControl)d;
            var oldLowestAlpha = (Int32)e.OldValue;
            var newLowestAlpha = sprocket.LowestAlpha;
            sprocket.OnLowestAlphaChanged(oldLowestAlpha, newLowestAlpha);
        }

        /// <summary>
        /// Provides derived classes an opportunity to handle changes to the LowestAlpha property.
        /// </summary>
        protected void OnLowestAlphaChanged(Int32 oldLowestAlpha, Int32 newLowestAlpha)
        {
        }

        /// <summary>
        /// Coerces the LowestAlpha value.
        /// </summary>
        private static object CoerceLowestAlpha(DependencyObject d, object value)
        {
            var desiredLowestAlpha = (Int32)value;

            if (desiredLowestAlpha < AlphaLowerLimit)
                desiredLowestAlpha = AlphaLowerLimit;
            else if (desiredLowestAlpha > AlphaUpperLimit)
                desiredLowestAlpha = AlphaUpperLimit;

            return desiredLowestAlpha;
        }

        #endregion

        #region OuterRadius

        /// <summary>
        /// OuterRadius Dependency Property
        /// </summary>
        public static readonly DependencyProperty OuterRadiusProperty =
            DependencyProperty.Register("OuterRadius", typeof(double), typeof(SprocketControl),
                new FrameworkPropertyMetadata(MinimumOuterRadiusFactor, OnOuterRadiusChanged, CoerceOuterRadius));

        /// <summary>
        /// Gets or sets the OuterRadius property. This dependency property 
        /// indicates the ratio of the Outer Width to the width of the SprocketControl.
        /// </summary>
        public double OuterRadius
        {
            get => (double)GetValue(OuterRadiusProperty);
            set => SetValue(OuterRadiusProperty, value);
        }

        /// <summary>
        /// Handles changes to the OuterRadius property.
        /// </summary>
        private static void OnOuterRadiusChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var sprocket = (SprocketControl)d;
            sprocket.OnOuterRadiusChanged();
        }

        /// <summary>
        /// Provides derived classes an opportunity to handle changes to the OuterRadius property.
        /// </summary>
        protected void OnOuterRadiusChanged()
        {
            // Recalculate the spoke points
            CalculateSpokesPoints();
        }

        /// <summary>
        /// Coerces the OuterRadius value.
        /// </summary>
        private static object CoerceOuterRadius(DependencyObject d, object value)
        {
            var desiredOuterRadius = (double)value;

            return desiredOuterRadius;
        }

        #endregion

        #region Progress

        /// <summary>
        /// Progress Dependency Property
        /// </summary>
        public static readonly DependencyProperty ProgressProperty =
            DependencyProperty.Register("Progress", typeof(double), typeof(SprocketControl),
                new FrameworkPropertyMetadata(DefaultProgress, OnProgressChanged, CoerceProgress));

        /// <summary>
        /// Gets or sets the Progress property. This dependency property 
        /// indicates the progress percentage.
        /// </summary>
        public double Progress
        {
            get => (double)GetValue(ProgressProperty);
            set => SetValue(ProgressProperty, value);
        }

        /// <summary>
        /// Coerces the Progress value so that it stays in the range 0-100
        /// </summary>
        /// <param name="d">SprocketControl</param>
        /// <param name="value">New Value</param>
        /// <returns>Coerced Value</returns>
        private static object CoerceProgress(DependencyObject d, object value)
        {
            var progress = (double)value;

            if (progress < 0.0)
            {
                return 0.0;
            }

            return progress > 100.0 ? 100.0 : value;
        }

        /// <summary>
        /// Handles changes to the Progress property.
        /// </summary>
        /// <param name="d">SprocketControl</param>
        /// <param name="e">DependencyProperty changed event arguments</param>
        private static void OnProgressChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var sprocket = (SprocketControl)d;
            var oldProgress = (double)e.OldValue;
            var newProgress = sprocket.Progress;
            sprocket.OnProgressChanged(oldProgress, newProgress);
        }

        /// <summary>
        /// Provides derived classes an opportunity to handle changes to the Progress property.
        /// </summary>
        /// <param name="oldProgress">Old Value</param>
        /// <param name="newProgress">New Value</param>
        protected void OnProgressChanged(double oldProgress, double newProgress)
        {
            InvalidateVisual();
        }

        #endregion

        #region Rotation

        /// <summary>
        /// Rotation Dependency Property
        /// </summary>
        public static readonly DependencyProperty RotationProperty =
            DependencyProperty.Register("Rotation", typeof(Direction), typeof(SprocketControl),
                new FrameworkPropertyMetadata(Direction.Clockwise, OnRotationChanged));

        /// <summary>
        /// Gets or sets the Rotation property. This dependency property 
        /// indicates the direction of Rotation of the SprocketControl.
        /// </summary>
        public Direction Rotation
        {
            get => (Direction)GetValue(RotationProperty);
            set => SetValue(RotationProperty, value);
        }

        /// <summary>
        /// Handles changes to the Rotation property.
        /// </summary>
        /// <param name="d">SprocketControl</param>
        /// <param name="e">DependencyProperty changed event arguments</param>
        private static void OnRotationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var sprocket = (SprocketControl)d;
            var oldRotation = (Direction)e.OldValue;
            var newRotation = sprocket.Rotation;
            sprocket.OnRotationChanged(oldRotation, newRotation);
        }

        /// <summary>
        /// Provides derived classes an opportunity to handle changes to the Rotation property.
        /// </summary>
        /// <param name="oldRotation">Old Value</param>
        /// <param name="newRotation">New Value</param>
        protected void OnRotationChanged(Direction oldRotation, Direction newRotation)
        {
            // Recalculate the spoke points
            CalculateSpokesPoints();
        }

        #endregion

        #region StartAngle

        /// <summary>
        /// StartAngle Dependency Property
        /// </summary>
        public static readonly DependencyProperty StartAngleProperty =
            DependencyProperty.Register("StartAngle", typeof(double), typeof(SprocketControl),
                new FrameworkPropertyMetadata(DefaultStartAngle, OnStartAngleChanged));

        /// <summary>
        /// Gets or sets the StartAngle property. This dependency property 
        /// indicates the angle at which the first spoke (with max opacity) is drawn.
        /// </summary>
        public double StartAngle
        {
            get => (double)GetValue(StartAngleProperty);
            set => SetValue(StartAngleProperty, value);
        }

        /// <summary>
        /// Handles changes to the StartAngle property.
        /// </summary>
        /// <param name="d">SprocketControl</param>
        /// <param name="e">DependencyProperty changed event arguments</param>
        private static void OnStartAngleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var sprocket = (SprocketControl)d;
            var oldStartAngle = (double)e.OldValue;
            var newStartAngle = sprocket.StartAngle;
            sprocket.OnStartAngleChanged(oldStartAngle, newStartAngle);
        }

        /// <summary>
        /// Provides derived classes an opportunity to handle changes to the StartAngle property.
        /// </summary>
        /// <param name="oldStartAngle">Old Value</param>
        /// <param name="newStartAngle">New Value</param>
        protected void OnStartAngleChanged(double oldStartAngle, double newStartAngle)
        {
            // Recalculate the spoke points
            CalculateSpokesPoints();
        }

        #endregion

        #region TickColor

        /// <summary>
        /// TickColor Dependency Property
        /// </summary>
        public static readonly DependencyProperty TickColorProperty =
            DependencyProperty.Register("TickColor", typeof(Color), typeof(SprocketControl),
                new FrameworkPropertyMetadata(DefaultTickColor, OnTickColorChanged));

        /// <summary>
        /// Gets or sets the TickColor property. This dependency property 
        /// indicates the color of the Spokes in the SprocketControl.
        /// </summary>
        public Color TickColor
        {
            get => (Color)GetValue(TickColorProperty);
            set => SetValue(TickColorProperty, value);
        }

        /// <summary>
        /// Handles changes to the TickColor property.
        /// </summary>
        /// <param name="d">SprocketControl</param>
        /// <param name="e">DependencyProperty changed event arguments</param>
        private static void OnTickColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var sprocket = (SprocketControl)d;
            var oldTickColor = (Color)e.OldValue;
            var newTickColor = sprocket.TickColor;
            sprocket.OnTickColorChanged(oldTickColor, newTickColor);
        }

        /// <summary>
        /// Provides derived classes an opportunity to handle changes to the TickColor property.
        /// </summary>
        /// <param name="oldTickColor">Old Value</param>
        /// <param name="newTickColor">New Value</param>
        protected void OnTickColorChanged(Color oldTickColor, Color newTickColor)
        {
            InvalidateVisual();
        }

        #endregion

        #region TickCount

        /// <summary>
        /// TickCount Dependency Property
        /// </summary>
        public static readonly DependencyProperty TickCountProperty =
            DependencyProperty.Register("TickCount", typeof(int), typeof(SprocketControl),
                new FrameworkPropertyMetadata(DefaultTickCount, OnTickCountChanged, CoerceTickCount));

        /// <summary>
        /// Gets or sets the TickCount property. This dependency property 
        /// indicates the number of spokes of the SprocketControl.
        /// </summary>
        public int TickCount
        {
            get => (int)GetValue(TickCountProperty);
            set => SetValue(TickCountProperty, value);
        }

        /// <summary>
        /// Coerces the TickCount value to an acceptable value
        /// </summary>
        /// <param name="d">SprocketControl</param>
        /// <param name="value">New Value</param>
        /// <returns>Coerced Value</returns>
        private static object CoerceTickCount(DependencyObject d, object value)
        {
            return ((int)value <= 0) ? DefaultTickCount : value;
        }

        /// <summary>
        /// Handles changes to the TickCount property.
        /// </summary>
        /// <param name="d">SprocketControl</param>
        /// <param name="e">DependencyProperty changed event arguments</param>
        private static void OnTickCountChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var sprocket = (SprocketControl)d;
            var oldTickCount = (int)e.OldValue;
            var newTickCount = sprocket.TickCount;
            sprocket.OnTickCountChanged(oldTickCount, newTickCount);
        }

        /// <summary>
        /// Provides derived classes an opportunity to handle changes to the TickCount property.
        /// </summary>
        /// <param name="oldTickCount">Old Value</param>
        /// <param name="newTickCount">New Value</param>
        protected void OnTickCountChanged(int oldTickCount, int newTickCount)
        {
            // Recalculate the spoke points
            CalculateSpokesPoints();
        }

        #endregion

        #region TickStyle

        /// <summary>
        /// TickStyle Dependency Property
        /// </summary>
        public static readonly DependencyProperty TickStyleProperty =
            DependencyProperty.Register("TickStyle", typeof(PenLineCap), typeof(SprocketControl),
                new FrameworkPropertyMetadata(PenLineCap.Round, OnTickStyleChanged));

        /// <summary>
        /// Gets or sets the TickStyle property. This dependency property 
        /// indicates the style of the ends of each tick.
        /// </summary>
        public PenLineCap TickStyle
        {
            get => (PenLineCap)GetValue(TickStyleProperty);
            set => SetValue(TickStyleProperty, value);
        }

        /// <summary>
        /// Handles changes to the TickStyle property.
        /// </summary>
        /// <param name="d">SprocketControl</param>
        /// <param name="e">DependencyProperty changed event arguments</param>
        private static void OnTickStyleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var sprocket = (SprocketControl)d;
            var oldTickStyle = (PenLineCap)e.OldValue;
            var newTickStyle = sprocket.TickStyle;
            sprocket.OnTickStyleChanged(oldTickStyle, newTickStyle);
        }

        /// <summary>
        /// Provides derived classes an opportunity to handle changes to the TickStyle property.
        /// </summary>
        /// <param name="oldTickStyle">Old Value</param>
        /// <param name="newTickStyle">New Value</param>
        protected void OnTickStyleChanged(PenLineCap oldTickStyle, PenLineCap newTickStyle)
        {
            InvalidateVisual();
        }

        #endregion

        #region TickWidth

        /// <summary>
        /// TickWidth Dependency Property
        /// </summary>
        public static readonly DependencyProperty TickWidthProperty =
            DependencyProperty.Register("TickWidth", typeof(double), typeof(SprocketControl),
                new FrameworkPropertyMetadata(DefaultTickWidth, OnTickWidthChanged, CoerceTickWidth));

        /// <summary>
        /// Gets or sets the TickWidth property. This dependency property 
        /// indicates the width of each spoke in the SprocketControl.
        /// </summary>
        public double TickWidth
        {
            get => (double)GetValue(TickWidthProperty);
            set => SetValue(TickWidthProperty, value);
        }

        /// <summary>
        /// Coerces the TickWidth value so that it stays above 0.
        /// </summary>
        /// <param name="d">SprocketControl</param>
        /// <param name="value">New Value</param>
        /// <returns>Coerced Value</returns>
        private static object CoerceTickWidth(DependencyObject d, object value)
        {
            return ((double)value < 0.0) ? DefaultTickWidth : value;
        }
        /// <summary>
        /// Handles changes to the TickWidth property.
        /// </summary>
        /// <param name="d">SprocketControl</param>
        /// <param name="e">DependencyProperty changed event arguments</param>
        private static void OnTickWidthChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var target = (SprocketControl)d;
            var oldTickWidth = (double)e.OldValue;
            var newTickWidth = target.TickWidth;
            target.OnTickWidthChanged(oldTickWidth, newTickWidth);
        }

        /// <summary>
        /// Provides derived classes an opportunity to handle changes to the TickWidth property.
        /// </summary>
        /// <param name="oldTickWidth">Old Value</param>
        /// <param name="newTickWidth">New Value</param>
        protected void OnTickWidthChanged(double oldTickWidth, double newTickWidth)
        {
            InvalidateVisual();
        }

        #endregion

        #endregion

        #region Construction

        /// <summary>
        /// Ctor
        /// </summary>
        public SprocketControl()
        {
            renderTimer = new Timer(Interval);
            renderTimer.Elapsed += OnRenderTimerElapsed;

            // Set the minimum size of the SprocketControl
            //MinWidth = MINIMUM_CONTROL_SIZE.Width;
            //MinWidth = MINIMUM_CONTROL_SIZE.Height;

            // Calculate the spoke points based on the current size
            CalculateSpokesPoints();

            RoutedEventHandler handler = null;
            handler = async delegate
            {
                Loaded -= handler;
                await Dispatcher.InvokeAsync(() =>
                {
                    if ((IsIndeterminate) && (IsVisible))
                        Start();
                });
            };

            Loaded += handler;

            // Event handler added to stop the timer if the control is no longer visible
            IsVisibleChanged += OnVisibilityChanged;
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Start the Tick Control rotation
        /// </summary>
        private void Start()
        {
            if ((renderTimer == null) || (renderTimer.Enabled))
                return;

            renderTimer.Interval = Interval;
            renderTimer.Enabled = true;
        }

        /// <summary>
        /// Stop the Tick Control rotation
        /// </summary>
        private void Stop()
        {
            if (renderTimer != null)
            {
                renderTimer.Enabled = false;
            }
        }

        /// <summary>
        /// Converts Degrees to Radians
        /// </summary>
        /// <param name="degrees">Degrees</param>
        /// <returns>Radians</returns>
        private static double ConvertDegreesToRadians(double degrees)
        {
            return ((Math.PI / 180) * degrees);
        }

        /// <summary>
        /// Calculate the Spoke Points and store them
        /// </summary>
        private void CalculateSpokesPoints()
        {
            spokes = new List<Spoke>();

            // Calculate the angle between adjacent spokes
            angleIncrement = (360 / (double)TickCount);
            // Calculate the change in alpha between adjacent spokes
            alphaChange = (int)((255 - LowestAlpha) / ((AlphaTicksPercentage / 100.0) * TickCount));

            // Set the start angle for rendering
            renderStartAngle = StartAngle;

            // Calculate the location around which the spokes will be drawn
            var width = (Width < Height) ? Width : Height;
            centerPoint = new Point(Width / 2, Height / 2);
            // Calculate the inner and outer radii of the control. The radii should not be less than the
            // Minimum values
            //_innerRadius = (int)(width * INNER_RADIUS_FACTOR);
            //if (_innerRadius < MINIMUM_INNER_RADIUS)
            //    _innerRadius = MINIMUM_INNER_RADIUS;
            //_outerRadius = (int)(width * OUTER_RADIUS_FACTOR);
            //if (_outerRadius < MINIMUM_OUTER_RADIUS)
            //    _outerRadius = MINIMUM_OUTER_RADIUS;

            innerRadius = (int)(width * InnerRadius);
            outerRadius = (int)(width * OuterRadius);
            double angle = 0;

            for (var i = 0; i < TickCount; i++)
            {
                var pt1 = new Point(innerRadius * (float)Math.Cos(ConvertDegreesToRadians(angle)), innerRadius * (float)Math.Sin(ConvertDegreesToRadians(angle)));
                var pt2 = new Point(outerRadius * (float)Math.Cos(ConvertDegreesToRadians(angle)), outerRadius * (float)Math.Sin(ConvertDegreesToRadians(angle)));

                // Create a spoke based on the points generated
                var spoke = new Spoke(pt1, pt2);
                // Add the spoke to the List
                spokes.Add(spoke);

                // If it is not it Indeterminate state, 
                // ensure that the spokes are drawn in clockwise manner
                if (!IsIndeterminate)
                {
                    angle += angleIncrement;
                }
                else
                {
                    switch (Rotation)
                    {
                        case Direction.Clockwise:
                            angle -= angleIncrement;
                            break;
                        case Direction.Anticlockwise:
                            angle += angleIncrement;
                            break;
                    }
                }
            }
        }

        #endregion

        #region Overrides

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);

            // Calculate the spoke points based on the new size
            CalculateSpokesPoints();
        }

        protected override void OnRender(DrawingContext dc)
        {
            if (spokes == null)
                return;

            TranslateTransform translate = new TranslateTransform(centerPoint.X, centerPoint.Y);
            dc.PushTransform(translate);
            RotateTransform rotate = new RotateTransform(renderStartAngle);
            dc.PushTransform(rotate);

            var alpha = (byte)255;

            // Get the number of spokes that can be drawn with zero transparency
            var progressSpokes = (int)Math.Floor((Progress * TickCount) / 100.0);

            // Render the spokes
            for (var i = 0; i < TickCount; i++)
            {
                if (!IsIndeterminate)
                {
                    if (progressSpokes > 0)
                        alpha = (byte)(i < progressSpokes ? 255 : DefaultProgressAlpha);
                    else
                        alpha = (byte)DefaultProgressAlpha;
                }

                Pen p = new Pen(new SolidColorBrush(Color.FromArgb(alpha, TickColor.R, TickColor.G, TickColor.B)), TickWidth);
                p.StartLineCap = p.EndLineCap = TickStyle;
                dc.DrawLine(p, spokes[i].StartPoint, spokes[i].EndPoint);

                if (IsIndeterminate)
                {
                    alpha -= (byte)alphaChange;
                    if (alpha < LowestAlpha)
                        alpha = (byte)LowestAlpha;
                }
            }

            // Perform a reverse Rotation and Translation to obtain the original Transformation
            dc.Pop();
            dc.Pop();
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// Handles the Elapsed event of the renderTimer
        /// </summary>
        /// <param name="sender">Sender</param>
        /// <param name="e">EventArgs</param>
        private async void OnRenderTimerElapsed(object sender, ElapsedEventArgs e)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                switch (Rotation)
                {
                    case Direction.Clockwise:
                        renderStartAngle += angleIncrement;

                        if (renderStartAngle >= 360)
                            renderStartAngle -= 360;
                        break;
                    case Direction.Anticlockwise:
                        renderStartAngle -= angleIncrement;

                        if (renderStartAngle <= -360)
                            renderStartAngle += 360;
                        break;
                }

                // Force re-rendering of control
                InvalidateVisual();
            });
        }

        /// <summary>
        /// Event handler to stop the timer if the control is no longer visible
        /// and start it when it is visible
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnVisibilityChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            // Needs to be handled only if the state of the progress bar is indeterminate
            if (!IsIndeterminate)
                return;

            if ((bool)e.NewValue)
            {
                Start();
            }
            else
            {
                Stop();
            }
        }

        #endregion

        #region IDisposable Implementation

        /// <summary>
        /// Releases all resources used by an instance of the SprocketControl class.
        /// </summary>
        /// <remarks>
        /// This method calls the virtual Dispose(bool) method, passing in 'true', and then suppresses 
        /// finalization of the instance.
        /// </remarks>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases unmanaged resources before an instance of the SprocketControl class is reclaimed by garbage collection.
        /// </summary>
        /// <remarks>
        /// NOTE: Leave out the finalizer altogether if this class doesn't own unmanaged resources itself, 
        /// but leave the other methods exactly as they are.
        /// This method releases unmanaged resources by calling the virtual Dispose(bool), passing in 'false'.
        /// </remarks>
        ~SprocketControl()
        {
            Dispose(false);
        }

        /// <summary>
        /// Releases the unmanaged resources used by an instance of the SprocketControl class and optionally releases the managed resources.
        /// </summary>
        /// <param name="disposing">'true' to release both managed and unmanaged resources; 'false' to release only unmanaged resources.</param>
        protected void Dispose(bool disposing)
        {
            if (!disposing)
                return;
            if (renderTimer != null)
            {
                renderTimer.Elapsed -= OnRenderTimerElapsed;
                renderTimer.Dispose();
            }
            IsVisibleChanged -= OnVisibilityChanged;

            // free native resources if there are any.			
        }

        #endregion
    }
}
