﻿#nullable disable
using Core2D.Model.Renderer.Nodes;
using Core2D.ViewModels.Style;
using A = Avalonia;
using AM = Avalonia.Media;
using AP = Avalonia.Platform;

namespace Core2D.Modules.Renderer.Nodes
{
    internal class FillDrawNode : DrawNode, IFillDrawNode
    {
        public A.Rect Rect { get; set; }
        public BaseColorViewModel Color { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }

        public FillDrawNode(double x, double y, double width, double height, BaseColorViewModel color)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
            Color = color;
            UpdateGeometry();
        }

        public override void UpdateGeometry()
        {
            ScaleThickness = false;
            ScaleSize = false;
            Rect = new A.Rect(X, Y, Width, Height);
            Center = Rect.Center;
        }

        public override void UpdateStyle()
        {
            Fill = AvaloniaDrawUtil.ToBrush(Color);
        }

        public override void Draw(object dc, double zoom)
        {
            OnDraw(dc, zoom);
        }

        public override void OnDraw(object dc, double zoom)
        {
            var context = dc as AP.IDrawingContextImpl;
            context.DrawRectangle(Fill, null, Rect);
        }
    }
}
