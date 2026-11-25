// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Game.Screens.Edit;
using osu.Game.Rulesets.Catch.Edit;
using osu.Game.Rulesets.Catch.Edit.Blueprints;
using osu.Framework.Graphics;
using osu.Framework.Input;
using osu.Framework.Input.Events;
using osu.Framework.Utils;
using osu.Game.Rulesets.Catch.Edit.Blueprints.Components;
using osu.Game.Rulesets.Catch.Objects;
using osu.Game.Rulesets.Edit;
using osuTK;
using osuTK.Input;

namespace osu.Game.Rulesets.EditorAddons
{
	internal partial class FreehandJuicePlacement : CatchPlacementBlueprint<JuiceStream>
	{
		private readonly ScrollingPath scrollingPath;

		private readonly NestedOutlineContainer nestedOutlineContainer;

		private readonly PlacementEditablePath editablePath;

		private int lastEditablePathId = -1;
		[Resolved] private BindableBeatDivisor divisor { get; set; } = null!;
		[Resolved] private IBeatSnapProvider snap { get; set; } = null!;
		[Resolved] private EditorBeatmap beatmap { get; set; } = null!;
		private FreehandStroke? stroke;
		private bool drawing;

		// PlacementBlueprint discards the return value for non-button events.
		// Explicitly accept DragStart so the input manager keeps dispatching drag.
		protected override bool Handle(UIEvent e)
		{
			if (e is DragStartEvent start) return OnDragStart(start);
			return base.Handle(e);
		}

		protected override bool OnDragStart(DragStartEvent e)
		{
			if (e.Button != MouseButton.Left || PlacementActive != PlacementState.Active) return false;
			stroke = new FreehandStroke(HitObject.StartTime, HitObject.OriginalX,
				t => Grid.Next(beatmap.ControlPointInfo, divisor.Value, t));
			drawing = true;
			return true;
		}

		protected override void OnDrag(DragEvent e) => SampleStroke(e.ScreenSpaceMousePosition, false);

		protected override void OnDragEnd(DragEndEvent e)
		{
			if (!drawing) return;
			SampleStroke(e.ScreenSpaceMousePosition, true);
			drawing = false;
			EndPlacement(HitObject.Duration > 0.001);
		}

		private void SampleStroke(Vector2 position, bool finish)
		{
			if (stroke == null) return;
			double time = HitObjectContainer.TimeAtScreenSpacePosition(position);
			float x = Math.Clamp(HitObjectContainer.ToLocalSpace(position).X, 0, 512);
			stroke.Move(time, x);
			double end = snap.SnapTime(time, HitObject.StartTime);
			if (end <= HitObject.StartTime) end = Grid.Next(beatmap.ControlPointInfo, divisor.Value, HitObject.StartTime);
			var vertices = stroke.Points;
			if (finish) vertices = stroke.Finish(end, x);
			if (vertices.Count < 2) return;
			JuiceStreamPath path = new();
			foreach (FreehandStroke.Point point in vertices)
			{
				if (point.Time > HitObject.StartTime)
					path.Add(point.Time - HitObject.StartTime, point.X - HitObject.OriginalX);
			}
			double factor = HitObject.Velocity / HitObject.SliderVelocityMultiplier;
			HitObject.SliderVelocityMultiplier = Math.Clamp(Math.Ceiling(path.ComputeRequiredVelocity() / factor), 0.1, 10);
			ApplyDefaultsToHitObject();
			path.ConvertToSliderPath(HitObject.Path, HitObject.LegacyConvertedY, HitObject.Velocity);
			HitObject.Path.ExpectedDistance.Value = path.Duration * HitObject.Velocity;
			ApplyDefaultsToHitObject();
			scrollingPath.UpdatePathFrom(HitObjectContainer, HitObject);
			nestedOutlineContainer.UpdateNestedObjectsFrom(HitObjectContainer, HitObject);
		}

		private InputManager inputManager = null!;

		protected override bool IsValidForPlacement => base.IsValidForPlacement && (PlacementActive == PlacementState.Waiting || Precision.DefinitelyBigger(HitObject.Duration, 0));

		public FreehandJuicePlacement()
		{
			Children = new Drawable[]
			{
				scrollingPath = new ScrollingPath(),
				nestedOutlineContainer = new NestedOutlineContainer(),
				editablePath = new PlacementEditablePath(positionToTime)
			};
		}

		protected override void Update()
		{
			base.Update();

			if (PlacementActive == PlacementState.Active && !drawing)
				editablePath.UpdateFrom(HitObjectContainer, HitObject);
		}

		protected override void LoadComplete()
		{
			base.LoadComplete();

			inputManager = GetContainingInputManager()!;

			BeginPlacement();
		}

		protected override bool OnMouseDown(MouseDownEvent e)
		{
			switch (PlacementActive)
			{
				case PlacementState.Waiting:
					if (e.Button != MouseButton.Left) break;

					editablePath.AddNewVertex();
					BeginPlacement(true);
					return true;

				case PlacementState.Active:
					switch (e.Button)
					{
						case MouseButton.Left:
							editablePath.AddNewVertex();
							return true;

						case MouseButton.Right:
							EndPlacement(true);
							return true;
					}

					break;
			}

			return base.OnMouseDown(e);
		}

		public override SnapResult UpdateTimeAndPosition(Vector2 screenSpacePosition, double fallbackTime)
		{
			if (drawing) return new SnapResult(screenSpacePosition, HitObject.StartTime);
			var gridSnapResult = Composer?.FindSnappedPositionAndTime(screenSpacePosition) ?? new SnapResult(screenSpacePosition, fallbackTime);
			gridSnapResult.ScreenSpacePosition.X = screenSpacePosition.X;
			var distanceSnapResult = Composer?.TryDistanceSnap(gridSnapResult.ScreenSpacePosition);

			var result = distanceSnapResult != null && Vector2.Distance(gridSnapResult.ScreenSpacePosition, distanceSnapResult.ScreenSpacePosition) < CatchHitObjectComposer.DISTANCE_SNAP_RADIUS
				? distanceSnapResult
				: gridSnapResult;

			switch (PlacementActive)
			{
				case PlacementState.Waiting:
					HitObject.OriginalX = ToLocalSpace(result.ScreenSpacePosition).X;
					if (result.Time is double snappedTime)
						HitObject.StartTime = snappedTime;
					break;

				case PlacementState.Active:
					Vector2 unsnappedPosition = inputManager.CurrentState.Mouse.Position;
					editablePath.MoveLastVertex(unsnappedPosition);
					break;

				default:
					return result;
			}

			// Make sure the up-to-date position is used for outlines.
			Vector2 startPosition = CatchHitObjectUtils.GetStartPosition(HitObjectContainer, HitObject);
			editablePath.Position = nestedOutlineContainer.Position = scrollingPath.Position = startPosition;

			if (lastEditablePathId != editablePath.PathId)
				editablePath.UpdateHitObjectFromPath(HitObject);
			lastEditablePathId = editablePath.PathId;

			ApplyDefaultsToHitObject();
			scrollingPath.UpdatePathFrom(HitObjectContainer, HitObject);
			nestedOutlineContainer.UpdateNestedObjectsFrom(HitObjectContainer, HitObject);
			return result;
		}

		private double positionToTime(float relativeYPosition)
		{
			double time = HitObjectContainer.TimeAtPosition(relativeYPosition, HitObject.StartTime);
			return time - HitObject.StartTime;
		}
	}
}
