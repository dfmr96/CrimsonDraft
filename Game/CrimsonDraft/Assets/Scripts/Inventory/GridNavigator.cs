#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

namespace CrimsonDraft.Inventory
{
    public enum NavigationExit
    {
        None,
        Up,
    }

    public readonly struct GridLinks
    {
        public GridLinks(ContainerId? left, ContainerId? right, ContainerId? up, ContainerId? down)
        {
            this.Left  = left;
            this.Right = right;
            this.Up    = up;
            this.Down  = down;
        }

        public ContainerId? Left  { get; }
        public ContainerId? Right { get; }
        public ContainerId? Up    { get; }
        public ContainerId? Down  { get; }
    }

    public sealed class GridNavigator
    {
        private readonly Func<ContainerId, ItemContainer>            resolve;
        private readonly IReadOnlyDictionary<ContainerId, GridLinks> links;

        public GridNavigator(
            Func<ContainerId, ItemContainer> resolve,
            IReadOnlyDictionary<ContainerId, GridLinks> links,
            ContainerId start)
        {
            this.resolve = resolve;
            this.links   = links;
            this.Grid    = start;
        }

        public ContainerId Grid { get; private set; }
        public Vector2Int  Cell { get; private set; }

        public void Reset(ContainerId grid, Vector2Int cell)
        {
            var container = this.resolve(grid);
            this.Grid = grid;
            this.Cell = new Vector2Int(
                Mathf.Clamp(cell.x, 0, container.Width - 1),
                Mathf.Clamp(cell.y, 0, container.Height - 1));
        }

        public NavigationExit Move(Vector2Int direction, bool isHolding)
        {
            var container = this.resolve(this.Grid);
            var next      = this.Cell + new Vector2Int(direction.x, -direction.y);

            if (!isHolding && container.GetItemAt(this.Cell) is { } under)
            {
                var placement = container.GetPlacement(under)!;
                if      (direction.x > 0) next.x = placement.Origin.x + placement.Footprint.x;
                else if (direction.x < 0) next.x = placement.Origin.x - 1;
                else if (direction.y < 0) next.y = placement.Origin.y + placement.Footprint.y;
                else if (direction.y > 0) next.y = placement.Origin.y - 1;
            }

            var gridLinks = this.links.TryGetValue(this.Grid, out var found) ? found : default;

            if (next.y < 0)
            {
                if (gridLinks.Up is { } up) { EnterVertical(up, next.x, atBottom: true); return NavigationExit.None; }
                if (!isHolding) return NavigationExit.Up;
                next.y = container.Height - 1;
            }
            else if (next.y >= container.Height)
            {
                if (gridLinks.Down is { } down) { EnterVertical(down, next.x, atBottom: false); return NavigationExit.None; }
                next.y = 0;
            }

            if (next.x < 0)
            {
                if (gridLinks.Left is { } left) { EnterHorizontal(left, next.y, atRightEdge: true); return NavigationExit.None; }
                next.x = 0;
            }
            else if (next.x >= container.Width)
            {
                if (gridLinks.Right is { } right) { EnterHorizontal(right, next.y, atRightEdge: false); return NavigationExit.None; }
                next.x = container.Width - 1;
            }

            this.Cell = next;
            return NavigationExit.None;
        }

        private void EnterVertical(ContainerId target, int column, bool atBottom)
        {
            var container = this.resolve(target);
            this.Grid = target;
            this.Cell = new Vector2Int(Mathf.Clamp(column, 0, container.Width - 1), atBottom ? container.Height - 1 : 0);
        }

        private void EnterHorizontal(ContainerId target, int row, bool atRightEdge)
        {
            var container = this.resolve(target);
            this.Grid = target;
            this.Cell = new Vector2Int(atRightEdge ? container.Width - 1 : 0, Mathf.Clamp(row, 0, container.Height - 1));
        }
    }
}
