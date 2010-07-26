﻿// <file>
//     <copyright see="prj:///doc/copyright.txt"/>
//     <license see="prj:///doc/license.txt"/>
//     <owner name="Martin Koníček" email="martin.konicek@gmail.com"/>
//     <version>$Revision$</version>
// </file>

using System;
using Debugger.AddIn.Visualizers.Graph.Drawing;

namespace Debugger.AddIn.Visualizers.Graph.Layout
{
	/// <summary>
	/// TreeNode used in TB layout mode.
	/// </summary>
	public class TreeNodeTB : TreeGraphNode
	{
		public TreeNodeTB(ObjectGraphNode objectNode) : base(objectNode)
		{
		}
		
		public override double MainSize { get { return this.Height; } }
		public override double LateralSize { get { return this.Width; } }
		
		public override double MainCoord { get { return this.Top; } set { this.Top = value; } }
		public override double LateralCoord { get { return this.Left; } set { this.Left = value; } }
		
		public override double MainMargin { get { return this.VerticalMargin; } }
		public override double LateralMargin { get { return this.HorizontalMargin; } }
	}
}