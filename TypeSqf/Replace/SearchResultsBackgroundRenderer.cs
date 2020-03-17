
using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;
using ICSharpCode.AvalonEdit.Search;

namespace TypeSqf.Edit.Replace
{
    public class SearchResultsBackgroundRenderer : IBackgroundRenderer
    {
        TextSegmentCollection<SearchResult> currentResults = new TextSegmentCollection<SearchResult>();
        private Brush markerBrush;
        private Pen markerPen;

        public TextSegmentCollection<SearchResult> CurrentResults
        {
            get { return currentResults; }
        }

        private KnownLayer _layer;
        public KnownLayer Layer
        {
            get
            {
                return _layer;
            }
            set
            {
                _layer = value;
            }
        }

        public SearchResultsBackgroundRenderer()
        {
            Layer = KnownLayer.Selection;
            markerBrush = new SolidColorBrush(Colors.Orange);
            markerBrush.Opacity = 0.5;
            markerPen = new Pen(markerBrush, 1);
        }

        public Brush MarkerBrush
        {
            get { return markerBrush; }
            set
            {
                this.markerBrush = value;
                markerPen = new Pen(markerBrush, 1);
            }
        }

        public void Draw(TextView textView, DrawingContext drawingContext)
        {
            if (textView == null)
                throw new ArgumentNullException("textView");
            if (drawingContext == null)
                throw new ArgumentNullException("drawingContext");

            if (currentResults == null || !textView.VisualLinesValid)
                return;

            var visualLines = textView.VisualLines;
            if (visualLines.Count == 0)
                return;

            int viewStart = visualLines.First().FirstDocumentLine.Offset;
            int viewEnd = visualLines.Last().LastDocumentLine.EndOffset;

            foreach (SearchResult result in currentResults.FindOverlappingSegments(viewStart, viewEnd - viewStart))
            {
                BackgroundGeometryBuilder geoBuilder = new BackgroundGeometryBuilder();
                geoBuilder.AlignToMiddleOfPixels = true;
                geoBuilder.CornerRadius = 3;
                geoBuilder.AddSegment(textView, result);
                Geometry geometry = geoBuilder.CreateGeometry();
                if (geometry != null)
                {
                    drawingContext.DrawGeometry(markerBrush, markerPen, geometry);
                }
            }
        }
    }

    public class SearchResult : TextSegment, ISearchResult
    {
        public SearchResult(Match match)
        {
            this.Data = match;
            this.StartOffset = match.Index;
            this.Length = match.Length;
        }
        public SearchResult(int startOffset, int length)
        {
            this.StartOffset = startOffset;
            this.Length = length;
        }

        public void MoveForward(int offsetToAdd)
        {
            this.StartOffset = this.StartOffset + offsetToAdd;
        }

        public Match Data { get; set; }

        public string ReplaceWith(string replacement)
        {
            return Data.Result(replacement);
        }
    }
}
