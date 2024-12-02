using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;
using GraphProcessor;
using Latios.Kinemation.Graph.Asset;

namespace Latios.Kinemation.Graph.Editor
{
	[NodeCustomEditor(typeof(FinalPoseNode))]
	public class FinalPoseView : BaseNodeView
	{
	}
}
