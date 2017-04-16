﻿using OpenNos.Pathfinding;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace OpenNos.PathFinder
{
    public class BestFirstSearch
    {
        #region Methods

        public static List<GridPos> findPath(GridPos start, GridPos end, GridPos[,] Grid)
        {
            Node[,] grid = new Node[Grid.GetLength(0), Grid.GetLength(1)];
            for (short y = 0; y < grid.GetLength(1); y++)
            {
                for (short x = 0; x < grid.GetLength(0); x++)
                {
                    grid[x, y] = new Node()
                    {
                        Value = Grid[x, y].Value,
                        X = x,
                        Y = y
                    };
                }
            }
            Node node = new Node();
            Node Start = grid[start.X, start.Y];
            MinHeap path = new MinHeap();

            // push the start node into the open list
            path.Push(Start);
            Start.Opened = true;

            // while the open list is not empty
            while (path.Count > 0)
            {
                // pop the position of node which has the minimum `f` value.
                node = path.Pop();
                Grid[node.X, node.Y].Closed = true;

                //if reached the end position, construct the path and return it
                if (node.X == end.X && node.Y == end.Y)
                {
                    return Backtrace(node);
                }

                // get neigbours of the current node
                List<Node> neighbors = GetNeighbors(grid, node);

                for (int i = 0, l = neighbors.Count(); i < l; ++i)
                {
                    Node neighbor = neighbors[i];

                    if (neighbor.Closed)
                    {
                        continue;
                    }

                    // check if the neighbor has not been inspected yet, or can be reached with
                    // smaller cost from the current node
                    if (!neighbor.Opened)
                    {
                        if (neighbor.F == 0)
                        {
                            neighbor.F = Heuristic.Octile(Math.Abs(neighbor.X - end.X), Math.Abs(neighbor.Y - end.Y));
                        }

                        neighbor.Parent = node;

                        if (!neighbor.Opened)
                        {
                            path.Push(neighbor);
                            neighbor.Opened = true;
                        }
                        else
                        {
                            neighbor.Parent = node;
                        }
                    }
                }
            }
            return new List<GridPos>();
        }

        public static List<Node> GetNeighbors(Node[,] Grid, Node node)
        {
            short x = node.X,
                y = node.Y;
            List<Node> neighbors = new List<Node>();
            bool s0 = false, d0 = false,
             s1 = false, d1 = false,
             s2 = false, d2 = false,
             s3 = false, d3 = false;

            // ↑
            if (Grid[x, y - 1].IsWalkable())
            {
                neighbors.Add(Grid[x, y - 1]);
                s0 = true;
            }

            // →
            if (Grid[x + 1, y].IsWalkable())
            {
                neighbors.Add(Grid[x + 1, y]);
                s1 = true;
            }

            // ↓
            if (Grid[x, y + 1].IsWalkable())
            {
                neighbors.Add(Grid[x, y + 1]);
                s2 = true;
            }

            // ←
            if (Grid[x - 1, y].IsWalkable())
            {
                neighbors.Add(Grid[x - 1, y]);
                s3 = true;
            }

            d0 = s3 || s0;
            d1 = s0 || s1;
            d2 = s1 || s2;
            d3 = s2 || s3;

            // ↖
            if (d0 && Grid[x - 1, y - 1].IsWalkable())
            {
                neighbors.Add(Grid[x - 1, y - 1]);
            }

            // ↗
            if (d1 && Grid[x + 1, y - 1].IsWalkable())
            {
                neighbors.Add(Grid[x + 1, y - 1]);
            }

            // ↘
            if (d2 && Grid[x + 1, y + 1].IsWalkable())
            {
                neighbors.Add(Grid[x + 1, y + 1]);
            }

            // ↙
            if (d3 && Grid[x - 1, y + 1].IsWalkable())
            {
                neighbors.Add(Grid[x - 1, y + 1]);
            }

            return neighbors;
        }

        public static void ShowGrid(Node[,] grid)
        {
            Console.Clear();
            Console.Write(string.Concat(Enumerable.Repeat("-", grid.GetLength(0))));
            for (int y = 0; y < grid.GetLength(1); y++)
            {
                for (int x = 0; x < grid.GetLength(0); x++)
                {
                    string value = grid[x, y].IsWalkable() ? " " : "X";
                    if (value == " ")
                    {
                        if (grid[x, y].Opened)
                        {
                            value = "O";
                        }
                        else if (grid[x, y].Closed)
                        {
                            value = "C";
                        }
                    }
                    if (x == 0)
                    {
                        Console.Write("|");
                    }
                    else
                    {
                        Console.Write(value);
                    }
                }
                if (y != 0)
                {
                    Console.Write("|");
                }
                Console.Write("\n");
            }
            Console.Write(string.Concat(Enumerable.Repeat("-", grid.GetLength(0))));
            Thread.Sleep(1000);
        }

        private static List<GridPos> Backtrace(Node end)
        {
            List<GridPos> path = new List<GridPos>();
            while (end.Parent != null)
            {
                end = end.Parent;
                path.Add(end);
            }
            path.Reverse();
            return path;
        }

        #endregion
    }
}