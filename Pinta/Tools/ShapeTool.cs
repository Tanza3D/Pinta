// 
// ShapeTool.cs
//  
// Author:
//       Jonathan Pobst <monkey@jpobst.com>
// 
// Copyright (c) 2010 Jonathan Pobst
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using Cairo;
using Pinta.Core;

namespace Pinta
{
	public abstract class ShapeTool : BaseTool
	{
		protected bool is_drawing = false;
		protected PointD shape_origin;
		protected Color outline_color;
		protected Color fill_color;

		protected ToolBarComboBox brush_width;
		protected ToolBarLabel brush_width_label;
		protected ToolBarButton brush_width_minus;
		protected ToolBarButton brush_width_plus;

		protected Rectangle last_dirty;
		protected ImageSurface undo_surface;
		
		public ShapeTool ()
		{
		}

		#region Properties
		protected int BrushWidth {
			get { return int.Parse (brush_width.ComboBox.ActiveText); }
			set { (brush_width.ComboBox as Gtk.ComboBoxEntry).Entry.Text = value.ToString (); }
		}
		#endregion
		
		#region ToolBar
		protected override void OnBuildToolBar (Gtk.Toolbar tb)
		{
			base.OnBuildToolBar (tb);
			
			BuildToolBar (tb);
		}

		// Do this in a separate method so SelectTool can override it as 
		// a no-op, but still get the BaseShape.OnBuildToolBar logic.
		protected virtual void BuildToolBar (Gtk.Toolbar tb)
		{
			if (brush_width_label == null)
				brush_width_label = new ToolBarLabel (" Brush width: ");
			
			tb.AppendItem (brush_width_label);
			
			if (brush_width_minus == null) {
				brush_width_minus = new ToolBarButton ("Toolbar.MinusButton.png", "", "Decrease brush size");
				brush_width_minus.Clicked += MinusButtonClickedEvent;
			}
			
			tb.AppendItem (brush_width_minus);
			
			if (brush_width == null)
				brush_width = new ToolBarComboBox (50, 1, true, "1", "2", "3", "4", "5", "6", "7", "8", "9",
				"10", "11", "12", "13", "14", "15", "20", "25", "30", "35",
				"40", "45", "50", "55");
			
			tb.AppendItem (brush_width);
			
			if (brush_width_plus == null) {
				brush_width_plus = new ToolBarButton ("Toolbar.PlusButton.png", "", "Increase brush size");
				brush_width_plus.Clicked += PlusButtonClickedEvent;
			}
			
			tb.AppendItem (brush_width_plus);
		}
		
		private void MinusButtonClickedEvent (object o, EventArgs args)
		{
			if (BrushWidth > 1)
				BrushWidth--;
		}

		private void PlusButtonClickedEvent (object o, EventArgs args)
		{
			BrushWidth++;
		}
		#endregion

		#region Mouse Handlers
		protected override void OnMouseDown (Gtk.DrawingArea canvas, Gtk.ButtonPressEventArgs args, Cairo.PointD point)
		{
			shape_origin = point;
			is_drawing = true;
			
			if (args.Event.Button == 1) {
				outline_color = PintaCore.Palette.PrimaryColor;
				fill_color = PintaCore.Palette.SecondaryColor;
			} else {
				outline_color = PintaCore.Palette.SecondaryColor;
				fill_color = PintaCore.Palette.PrimaryColor;
			}
			
			PintaCore.Layers.ToolLayer.Clear ();
			PintaCore.Layers.ToolLayer.Hidden = false;
			
			undo_surface = PintaCore.Layers.CurrentLayer.Surface.Clone ();
		}

		protected override void OnMouseUp (Gtk.DrawingArea canvas, Gtk.ButtonReleaseEventArgs args, Cairo.PointD point)
		{
			double x = point.X;
			double y = point.Y;
			
			PintaCore.Layers.ToolLayer.Hidden = true;
			
			DrawShape (PointsToRectangle (shape_origin, new PointD (x, y), (args.Event.State & Gdk.ModifierType.ShiftMask) == Gdk.ModifierType.ShiftMask), PintaCore.Layers.CurrentLayer);
			
			Gdk.Rectangle r = GetRectangleFromPoints (shape_origin, new PointD (x, y));
			PintaCore.Workspace.InvalidateRect (last_dirty.ToGdkRectangle (), false);
			
			is_drawing = false;
			
			PintaCore.History.PushNewItem (CreateHistoryItem ());
		}

		protected override void OnMouseMove (object o, Gtk.MotionNotifyEventArgs args, Cairo.PointD point)
		{
			if (!is_drawing)
				return;
			
			double x = point.X;
			double y = point.Y;
			
			PintaCore.Layers.ToolLayer.Clear ();
			
			Rectangle dirty = DrawShape (PointsToRectangle (shape_origin, new PointD (x, y), (args.Event.State & Gdk.ModifierType.ShiftMask) == Gdk.ModifierType.ShiftMask), PintaCore.Layers.ToolLayer);
			dirty = dirty.Clamp ();
			
			PintaCore.Workspace.InvalidateRect (last_dirty.ToGdkRectangle (), false);
			PintaCore.Workspace.InvalidateRect (dirty.ToGdkRectangle (), false);
			
			last_dirty = dirty;
		}
		#endregion

		#region Virtual Methods
		protected virtual Rectangle DrawShape (Rectangle r, Layer l)
		{
			return r;
		}
		
		protected virtual BaseHistoryItem CreateHistoryItem ()
		{
			return new SimpleHistoryItem (Icon, Name, undo_surface, PintaCore.Layers.CurrentLayerIndex);
		}
		#endregion

		#region Protected Methods
		protected Gdk.Rectangle GetRectangleFromPoints (PointD a, PointD b)
		{
			int x = (int)Math.Min (a.X, b.X) - BrushWidth - 2;
			int y = (int)Math.Min (a.Y, b.Y) - BrushWidth - 2;
			int w = (int)Math.Max (a.X, b.X) - x + (BrushWidth * 2) + 4;
			int h = (int)Math.Max (a.Y, b.Y) - y + (BrushWidth * 2) + 4;
			
			return new Gdk.Rectangle (x, y, w, h);
		}

		protected Rectangle PointsToRectangle (PointD p1, PointD p2, bool constrain)
		{
			// We want to create a rectangle that always has positive width/height
			double x, y, w, h;
			
			if (p1.Y <= p2.Y) {
				y = p1.Y;
				h = p2.Y - y;
			} else {
				y = p2.Y;
				h = p1.Y - y;
			}
			
			if (p1.X <= p2.X) {
				x = p1.X;
				
				if (constrain)
					w = h;
				else
					w = p2.X - x;
			} else {
				x = p2.X;
				
				if (constrain) {
					w = h;
					x = p1.X - w;
				} else
					w = p1.X - x;
			}

			return new Rectangle (x, y, w, h);
		}
		#endregion
	}
}
