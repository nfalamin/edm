using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace EDM.Views
{
    /// <summary>
    /// OverviewStatsCard.xaml - Individual KPI card component with icon, value, subtitle, and visual indicator
    /// </summary>
    public partial class OverviewStatsCard : System.Windows.Controls.UserControl
    {
        public OverviewStatsCard()
        {
            InitializeComponent();
        }

        #region Dependency Properties

        public string CardTitle
        {
            get { return (string)GetValue(CardTitleProperty); }
            set { SetValue(CardTitleProperty, value); }
        }
        public static readonly DependencyProperty CardTitleProperty =
            DependencyProperty.Register("CardTitle", typeof(string), typeof(OverviewStatsCard), 
                new PropertyMetadata(""));

        public string CardValueText
        {
            get { return (string)GetValue(CardValueTextProperty); }
            set { SetValue(CardValueTextProperty, value); }
        }
        public static readonly DependencyProperty CardValueTextProperty =
            DependencyProperty.Register("CardValueText", typeof(string), typeof(OverviewStatsCard), 
                new PropertyMetadata("0", OnCardValueChanged));

        public string CardSubtitleText
        {
            get { return (string)GetValue(CardSubtitleTextProperty); }
            set { SetValue(CardSubtitleTextProperty, value); }
        }
        public static readonly DependencyProperty CardSubtitleTextProperty =
            DependencyProperty.Register("CardSubtitleText", typeof(string), typeof(OverviewStatsCard), 
                new PropertyMetadata("Subtitle"));

        public string CardIconSymbol
        {
            get { return (string)GetValue(CardIconSymbolProperty); }
            set { SetValue(CardIconSymbolProperty, value); }
        }
        public static readonly DependencyProperty CardIconSymbolProperty =
            DependencyProperty.Register("CardIconSymbol", typeof(string), typeof(OverviewStatsCard), 
                new PropertyMetadata("☁", OnCardIconChanged));

        public System.Windows.Media.Brush IconGradientStart
        {
            get { return (System.Windows.Media.Brush)GetValue(IconGradientStartProperty); }
            set { SetValue(IconGradientStartProperty, value); }
        }
        public static readonly DependencyProperty IconGradientStartProperty =
            DependencyProperty.Register("IconGradientStart", typeof(System.Windows.Media.Brush), typeof(OverviewStatsCard), 
                new PropertyMetadata(new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 139, 92, 246)), OnIconGradientChanged));

        public System.Windows.Media.Brush IconGradientEnd
        {
            get { return (System.Windows.Media.Brush)GetValue(IconGradientEndProperty); }
            set { SetValue(IconGradientEndProperty, value); }
        }
        public static readonly DependencyProperty IconGradientEndProperty =
            DependencyProperty.Register("IconGradientEnd", typeof(System.Windows.Media.Brush), typeof(OverviewStatsCard), 
                new PropertyMetadata(new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 109, 40, 217)), OnIconGradientChanged));

        public double IndicatorProgress
        {
            get { return (double)GetValue(IndicatorProgressProperty); }
            set { SetValue(IndicatorProgressProperty, value); }
        }
        public static readonly DependencyProperty IndicatorProgressProperty =
            DependencyProperty.Register("IndicatorProgress", typeof(double), typeof(OverviewStatsCard), 
                new PropertyMetadata(50.0, OnIndicatorProgressChanged));

        #endregion

        #region Property Changed Handlers

        private static void OnCardValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = d as OverviewStatsCard;
            if (control != null && control.CardValue != null)
            {
                control.CardValue.Text = (string)e.NewValue;
            }
        }

        private static void OnCardIconChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = d as OverviewStatsCard;
            if (control != null && control.CardIcon != null)
            {
                control.CardIcon.Text = (string)e.NewValue;
            }
        }

        private static void OnIconGradientChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = d as OverviewStatsCard;
            if (control != null && control.IconGradient != null)
            {
                // Recreate gradient with new colors
                var grad = control.IconGradient as LinearGradientBrush;
                if (grad != null && grad.GradientStops.Count >= 2)
                {
                    grad.GradientStops[0].Color = ((SolidColorBrush)control.IconGradientStart)?.Color ?? Colors.Purple;
                    grad.GradientStops[1].Color = ((SolidColorBrush)control.IconGradientEnd)?.Color ?? Colors.Indigo;
                }
            }
        }

        private static void OnIndicatorProgressChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = d as OverviewStatsCard;
            if (control != null && control.IndicatorBar != null)
            {
                double progress = (double)e.NewValue;
                // Clamp between 0 and 100
                progress = Math.Max(0, Math.Min(100, progress));
                // Set width based on percentage of container (40px is base, scale to container width ~300px)
                control.IndicatorBar.Width = (progress / 100.0) * 100; // Max 100px on ~300px card
            }
        }

        #endregion
    }
}
