﻿//Created 18.09.2010
//23.02.2013 - Old version was crap

using System.Collections.Generic;
using Hiale.GTA2NET.Core.Helper;
using Hiale.GTA2NET.Core.Map;
using Microsoft.Xna.Framework;

namespace Hiale.GTA2NET.Core.Collision
{
    /// <summary>
    /// Represents unpassable space
    /// </summary>
    public class MapCollision
    {
        private readonly Map.Map _map;

        public MapCollision(Map.Map map)
        {
            _map = map;
        }

        public List<IObstacle> CollisionMap(Vector2 start)
        {
            //Pass 1
            var blocks = FloodFill(start, CollisionMapType.Free);

            //Pass 2
            RemoveUnknownBlocks(blocks);

            //Pass 3
            var obstacles = new List<IObstacle>();
            FindLineObstacles(blocks, obstacles);

            for (var z = _map.Height - 1; z >= 0; z--)
            {
                for (var x = 0; x < _map.Width; x++)
                {
                    for (var y = 0; y < _map.Length; y++)
                    {
                        if (blocks[x, y, z] == CollisionMapType.Block)
                        {
                            obstacles.Add(new RectangleObstacle(new Vector2(x, y), z, 1, 1));
                        }
                        else if (blocks[x, y, z] == CollisionMapType.Slope)
                        {
                            if (!ProcessSlope(x, y, z, blocks, obstacles))
                                obstacles.Add(new SlopeObstacle(new Vector2(x, y), z, _map.CityBlocks[x, y, z].SlopeType));
                        }
                    }
                }

                for (var x = 0; x < _map.Width; x++)
                {
                    for (var y = 0; y < _map.Length; y++)
                    {
                        //if (blocks[x, y, z] == CollisionMapType.Block)
                        //    FindPolygons(blocks, new Vector2(x, y), z);
                    }
                }

                //var maxRect = new CollisionMapType[_map.Width, _map.Length];
                //for (var x = 0; x < _map.Width; x++)
                //{
                //    for (var y = 0; y < _map.Length; y++)
                //    {
                //        maxRect[y, x] = blocks[x, y, z]; //MaxSubmatrix' Raws/Columns are swapped
                //    }
                //}
                //SubMatrix.FindAllRectangles(blocks, z, obstacles);
                //var rect = SubMatrix.MaxSubmatrix(blocks, z, CollisionMapType.Block);
            }
            return obstacles;
        }

        private void FindPolygons(CollisionMapType[,,] blocks, Vector2 start, int z)
        {
            var stack = new Stack<Vector2>();
            var polygon = new PolygonObstacle(z);
            stack.Push(start);
            do
            {
                var currentPos = stack.Pop();
                var currentBlock = _map.CityBlocks[(int)currentPos.X, (int)currentPos.Y, z];
                //if (currentBlock.SlopeType == SlopeType.None)
                if (CheckBlockBounds(currentPos))
                {
                    var obstacles = new List<IObstacle>();
                    if (currentBlock.SlopeType == SlopeType.None || currentBlock.SlopeType == SlopeType.SlopeAbove)
                    {
                        if (!polygon.IsPointInPolygon(currentPos)) //top Left
                            polygon.Vertices.Add(currentPos);
                        var vector = new Vector2(currentPos.X, currentPos.Y);
                        if (!polygon.IsPointInPolygon(vector))
                            polygon.Vertices.Add(vector);
                        vector = new Vector2(currentPos.X + 1, currentPos.Y + 1);
                        if (!polygon.IsPointInPolygon(vector))
                            polygon.Vertices.Add(vector);
                        vector = new Vector2(currentPos.X, currentPos.Y + 1);
                        if (!polygon.IsPointInPolygon(vector))
                            polygon.Vertices.Add(vector);
                    }
                    else
                    {
                        ProcessSlope((int) currentPos.X, (int) currentPos.Y, z, blocks, obstacles);
                        foreach (var obstacle in obstacles)
                        {
                            if (obstacle is PolygonObstacle)
                            {
                                var polygonObstacle = (PolygonObstacle) obstacle;
                                foreach (var vertex in polygonObstacle.Vertices)
                                {
                                    if (!polygon.IsPointInPolygon(vertex))
                                        polygon.Vertices.Add(vertex);
                                    //Misc.IsPointInPolygon()
                                }
                            }
                        }
                    }
                    //if (currentBlock.IsEmpty)
                    //    blocks[(int)currentPos.X, (int)currentPos.Y, z] = typeToFill;
                    //else
                    //    blocks[(int)currentPos.X, (int)currentPos.Y, z] = CollisionMapType.Unknwon;
                }

                var newPos = new Vector2(currentPos.X + 1, currentPos.Y); //right
                if (CheckBlockBounds(newPos))
                {
                    if (CheckNeighborBlock((int) newPos.X, (int) newPos.Y, z, blocks))
                        stack.Push(newPos);
                }
                newPos = new Vector2(currentPos.X, currentPos.Y + 1); //bottom
                if (CheckBlockBounds(newPos))
                {
                    if (CheckNeighborBlock((int)newPos.X, (int)newPos.Y, z, blocks))
                        stack.Push(newPos);
                }
                newPos = new Vector2(currentPos.X - 1, currentPos.Y); //left
                if (CheckBlockBounds(newPos))
                {
                    if (CheckNeighborBlock((int)newPos.X, (int)newPos.Y, z, blocks))
                        stack.Push(newPos);
                }
                newPos = new Vector2(currentPos.X, currentPos.Y - 1); //top
                if (CheckBlockBounds(newPos))
                {
                    if (CheckNeighborBlock((int)newPos.X, (int)newPos.Y, z, blocks))
                        stack.Push(newPos);
                }
            } while (stack.Count > 0);
            
        }

        private bool CheckNeighborBlock(int x, int y, int z, CollisionMapType[, ,] blocks)
        {
            if (blocks[x, y, z] == CollisionMapType.Block)
                return true;
            return false;

        }

        private bool ProcessSlope(int x, int y, int z, CollisionMapType[,,] blocks, List<IObstacle> obstacles)
        {
            var block = _map.CityBlocks[x, y, z];

            CollisionMapType blockLeft = x - 1 > 0 ? blocks[x - 1, y, z] : CollisionMapType.Unknwon;
            CollisionMapType blockTop = y - 1 > 0 ? blocks[x, y - 1, z] : CollisionMapType.Unknwon;
            CollisionMapType blockRight = x + 1 < _map.Width ? blocks[x + 1, y, z] : CollisionMapType.Unknwon;
            CollisionMapType blockBottom = y + 1 < _map.Length ? blocks[x, y + 1, z] : CollisionMapType.Unknwon;

            if ((byte) block.SlopeType > 44)
                blocks[x, y, z] = CollisionMapType.Block;
            else
                return false;

            switch (block.SlopeType)
            {
                case SlopeType.DiagonalFacingUpLeft: //45
                    if ((blockBottom == CollisionMapType.Block && blockRight == CollisionMapType.Block) ||
                        (block.Right && block.Bottom))
                    {
                        var obstacle = new PolygonObstacle(z);
                        obstacle.Vertices.Add(new Vector2(x, y + 1));
                        obstacle.Vertices.Add(new Vector2(x + 1, y));
                        obstacle.Vertices.Add(new Vector2(x + 1, y + 1));
                        obstacles.Add(obstacle);
                    }
                    else
                    {
                        if (block.Left && block.Left.Wall)
                            obstacles.Add(new LineObstacle(new Vector2(x, y + 1), new Vector2(x + 1, y),
                                                           z, LineObstacleType.Other));
                        if (block.Top && block.Top.Wall)
                            obstacles.Add(LineObstacle.DefaultTop(x, y, z));
                        if (block.Right && block.Right.Wall)
                            obstacles.Add(LineObstacle.DefaultRight(x, y, z));
                        if (block.Bottom && block.Bottom.Wall)
                            obstacles.Add(LineObstacle.DefaultBottom(x, y, z));
                    }
                    return true;
                case SlopeType.DiagonalFacingUpRight: //46
                    if ((blockBottom == CollisionMapType.Block && blockLeft == CollisionMapType.Block) ||
                        (block.Left && block.Bottom))
                    {
                        var obstacle = new PolygonObstacle(z);
                        obstacle.Vertices.Add(new Vector2(x, y));
                        obstacle.Vertices.Add(new Vector2(x + 1, y + 1));
                        obstacle.Vertices.Add(new Vector2(x, y + 1));
                        obstacles.Add(obstacle);
                    }
                    else
                    {
                        if (block.Left && block.Left.Wall)
                            obstacles.Add(LineObstacle.DefaultLeft(x, y, z));
                        if (block.Top && block.Top.Wall)
                            obstacles.Add(LineObstacle.DefaultTop(x, y, z));
                        if (block.Right && block.Right.Wall)
                            obstacles.Add(new LineObstacle(new Vector2(x, y), new Vector2(x + 1, y + 1),
                                                           z, LineObstacleType.Other));
                        if (block.Bottom && block.Bottom.Wall)
                            obstacles.Add(LineObstacle.DefaultBottom(x, y, z));
                    }
                    return true;
                case SlopeType.DiagonalFacingDownLeft: //47
                    if ((blockTop == CollisionMapType.Block && blockRight == CollisionMapType.Block) ||
                        (block.Top && block.Right))
                    {
                        var obstacle = new PolygonObstacle(z);
                        obstacle.Vertices.Add(new Vector2(x, y));
                        obstacle.Vertices.Add(new Vector2(x + 1, y));
                        obstacle.Vertices.Add(new Vector2(x + 1, y + 1));
                        obstacles.Add(obstacle);
                    }
                    else
                    {
                        if (block.Left && block.Left.Wall)
                            obstacles.Add(new LineObstacle(new Vector2(x, y), new Vector2(x + 1, y + 1),
                                                           z, LineObstacleType.Other));
                        if (block.Top && block.Top.Wall)
                            obstacles.Add(LineObstacle.DefaultTop(x, y, z));
                        if (block.Right && block.Right.Wall)
                            obstacles.Add(LineObstacle.DefaultRight(x, y, z));
                        if (block.Bottom && block.Bottom.Wall)
                            obstacles.Add(LineObstacle.DefaultBottom(x, y, z));
                    }
                    return true;
                case SlopeType.DiagonalFacingDownRight: //48
                    if ((blockTop == CollisionMapType.Block && blockLeft == CollisionMapType.Block) ||
                        (block.Left && block.Top))
                    {
                        var obstacle = new PolygonObstacle(z);
                        obstacle.Vertices.Add(new Vector2(x, y));
                        obstacle.Vertices.Add(new Vector2(x + 1, y));
                        obstacle.Vertices.Add(new Vector2(x, y + 1));
                        obstacles.Add(obstacle);
                    }
                    else
                    {
                        if (block.Left && block.Left.Wall)
                            obstacles.Add(LineObstacle.DefaultLeft(x, y, z));
                        if (block.Top && block.Top.Wall)
                            obstacles.Add(LineObstacle.DefaultTop(x, y, z));
                        if (block.Right && block.Right.Wall)
                            obstacles.Add(new LineObstacle(new Vector2(x, y + 1), new Vector2(x + 1, y),
                                                           z, LineObstacleType.Other));
                        if (block.Bottom && block.Bottom.Wall)
                            obstacles.Add(LineObstacle.DefaultBottom(x, y, z));
                    }
                    return true;
            }
            return false;
        }

        #region ToDo
            //if (block.Left && block.Left.Wall)
            //{
            //    processedCount++;
            //    switch (block.SlopeType)
            //    {
            //        case SlopeType.DiagonalFacingUpLeft:
            //        case SlopeType.DiagonalSlopeFacingUpLeft:
            //            obstacles.Add(new LineObstacle(z, new Vector2(x, y + 1), new Vector2(x + 1, y), LineObstacleType.Other));
            //            break;
            //        case SlopeType.DiagonalFacingDownLeft:
            //        case SlopeType.DiagonalSlopeFacingDownLeft:
            //            obstacles.Add(new LineObstacle(z, new Vector2(x, y), new Vector2(x + 1, y + 1), LineObstacleType.Other));
            //            break;
            //        case SlopeType.PartialBlockRight:
            //            obstacles.Add(new LineObstacle(z, new Vector2(x + 1 - BlockInfo.PartialBlockScalar, y), new Vector2(x + 1, y), LineObstacleType.Vertical));
            //            break;
            //        case SlopeType.PartialBlockTop:
            //        case SlopeType.PartialBlockTopLeft:
            //            obstacles.Add(new LineObstacle(z, new Vector2(x, y), new Vector2(x, y + BlockInfo.PartialBlockScalar), LineObstacleType.Vertical));
            //            break;
            //        case SlopeType.PartialBlockBottom:
            //        case SlopeType.PartialBlockBottomLeft:
            //            obstacles.Add(new LineObstacle(z, new Vector2(x, y + 1 - BlockInfo.PartialBlockScalar), new Vector2(x, y + 1), LineObstacleType.Vertical));
            //            break;
            //        case SlopeType.PartialBlockTopRight:
            //            obstacles.Add(new LineObstacle(z, new Vector2(x + 1 - BlockInfo.PartialBlockScalar, y), new Vector2(x + 1 - BlockInfo.PartialBlockScalar, y + BlockInfo.PartialBlockScalar), LineObstacleType.Vertical));
            //            break;
            //        case SlopeType.PartialBlockBottomRight:
            //            obstacles.Add(new LineObstacle(z, new Vector2(x + 1 - BlockInfo.PartialBlockScalar, y + 1 - BlockInfo.PartialBlockScalar), new Vector2(x + 1 - BlockInfo.PartialBlockScalar, y + 1), LineObstacleType.Vertical));
            //            break;
            //        default:
            //            processedCount--;
            //            break;
            //    }
            //}
            //if (block.Top && block.Top.Wall)
            //{
            //    processedCount++;
            //    switch (block.SlopeType)
            //    {
            //        case SlopeType.PartialBlockLeft:
            //        case SlopeType.PartialBlockTopLeft:
            //            obstacles.Add(new LineObstacle(z, new Vector2(x, y), new Vector2(x + BlockInfo.PartialBlockScalar, y), LineObstacleType.Horizontal));
            //            break;
            //        case SlopeType.PartialBlockRight:
            //        case SlopeType.PartialBlockTopRight:
            //            obstacles.Add(new LineObstacle(z, new Vector2(x + 1 - BlockInfo.PartialBlockScalar, y), new Vector2(x + 1, y), LineObstacleType.Horizontal));
            //            break;
            //        case SlopeType.PartialBlockBottom:
            //            obstacles.Add(new LineObstacle(z, new Vector2(x, y + 1 - BlockInfo.PartialBlockScalar), new Vector2(x + 1, y + 1 - BlockInfo.PartialBlockScalar), LineObstacleType.Horizontal));
            //            break;
            //        case SlopeType.PartialBlockBottomRight:
            //            obstacles.Add(new LineObstacle(z, new Vector2(x + 1 - BlockInfo.PartialBlockScalar, y + 1 - BlockInfo.PartialBlockScalar), new Vector2(x + 1, y + 1 - BlockInfo.PartialBlockScalar), LineObstacleType.Horizontal));
            //            break;
            //        case SlopeType.PartialBlockBottomLeft:
            //            obstacles.Add(new LineObstacle(z, new Vector2(x, y + 1 - BlockInfo.PartialBlockScalar), new Vector2(x + BlockInfo.PartialBlockScalar, y + 1 - BlockInfo.PartialBlockScalar), LineObstacleType.Horizontal));
            //            break;
            //        default:
            //            processedCount--;
            //            break;
            //    }
            //}
            //if (block.Right && block.Right.Wall)
            //{
            //    processedCount++;
            //    switch (block.SlopeType)
            //    {
            //        case SlopeType.DiagonalFacingUpRight:
            //        case SlopeType.DiagonalSlopeFacingUpRight:
            //            obstacles.Add(new LineObstacle(z, new Vector2(x, y), new Vector2(x + 1, y + 1), LineObstacleType.Other));
            //            break;
            //        case SlopeType.DiagonalFacingDownRight:
            //        case SlopeType.DiagonalSlopeFacingDownRight:
            //            obstacles.Add(new LineObstacle(z, new Vector2(x, y + 1), new Vector2(x + 1, y), LineObstacleType.Other));
            //            break;
            //        case SlopeType.PartialBlockLeft:
            //            obstacles.Add(new LineObstacle(z, new Vector2(x + BlockInfo.PartialBlockScalar, y), new Vector2(x + BlockInfo.PartialBlockScalar, y + 1), LineObstacleType.Vertical));
            //            break;
            //        case SlopeType.PartialBlockTop:
            //        case SlopeType.PartialBlockTopRight:
            //            obstacles.Add(new LineObstacle(z, new Vector2(x + 1, y), new Vector2(x + 1, y + BlockInfo.PartialBlockScalar), LineObstacleType.Vertical));
            //            break;
            //        case SlopeType.PartialBlockBottom:
            //        case SlopeType.PartialBlockBottomRight:
            //            obstacles.Add(new LineObstacle(z, new Vector2(x + 1, y + 1 - BlockInfo.PartialBlockScalar), new Vector2(x + 1, y + 1), LineObstacleType.Vertical));
            //            break;
            //        case SlopeType.PartialBlockTopLeft:
            //            obstacles.Add(new LineObstacle(z, new Vector2(x + BlockInfo.PartialBlockScalar, y), new Vector2(x + BlockInfo.PartialBlockScalar, y + BlockInfo.PartialBlockScalar), LineObstacleType.Vertical));
            //            break;
            //        case SlopeType.PartialBlockBottomLeft:
            //            obstacles.Add(new LineObstacle(z, new Vector2(x + BlockInfo.PartialBlockScalar, y + 1 - BlockInfo.PartialBlockScalar), new Vector2(x + BlockInfo.PartialBlockScalar, y + 1), LineObstacleType.Vertical));
            //            break;
            //        default:
            //            processedCount--;
            //            break;
            //    }
            //}
            //if (block.Bottom && block.Bottom.Wall)
            //{
            //    processedCount++;
            //    switch (block.SlopeType)
            //    {
            //        case SlopeType.PartialBlockLeft:
            //        case SlopeType.PartialBlockBottomLeft:
            //            obstacles.Add(new LineObstacle(z, new Vector2(x, y + 1), new Vector2(x + BlockInfo.PartialBlockScalar, y + 1), LineObstacleType.Horizontal));
            //            break;
            //        case SlopeType.PartialBlockRight:
            //        case SlopeType.PartialBlockBottomRight:
            //            obstacles.Add(new LineObstacle(z, new Vector2(x + 1 - BlockInfo.PartialBlockScalar, y + 1), new Vector2(x + 1, y + 1), LineObstacleType.Horizontal));
            //            break;
            //        case SlopeType.PartialBlockTop:
            //            obstacles.Add(new LineObstacle(z, new Vector2(x, y + BlockInfo.PartialBlockScalar), new Vector2(x + 1, y + BlockInfo.PartialBlockScalar), LineObstacleType.Horizontal));
            //            break;
            //        case SlopeType.PartialBlockTopLeft:
            //            obstacles.Add(new LineObstacle(z, new Vector2(x, y + BlockInfo.PartialBlockScalar), new Vector2(x + BlockInfo.PartialBlockScalar, y + BlockInfo.PartialBlockScalar), LineObstacleType.Horizontal));
            //            break;
            //        case SlopeType.PartialBlockTopRight:
            //            obstacles.Add(new LineObstacle(z, new Vector2(x + 1 - BlockInfo.PartialBlockScalar, y + BlockInfo.PartialBlockScalar), new Vector2(x + 1, y + BlockInfo.PartialBlockScalar), LineObstacleType.Horizontal));
            //            break;
            //        default:
            //            processedCount--;
            //            break;
            //    }
            //}
            //if (processedCount > 0)
            //    return true;

            #endregion

        private bool CheckBlockBounds(Vector2 newPos)
        {
            return (newPos.X > -1) && (newPos.Y > -1) && (newPos.X < _map.Width) && (newPos.Y < _map.Length);
        }

        private void RemoveUnknownBlocks(CollisionMapType[, ,] blocks)
        {
            //Find slopes
            for (var z = _map.Height - 1; z >= 0; z--)
            {
                for (var x = 0; x < _map.Width; x++)
                {
                    for (var y = 0; y < _map.Length; y++)
                    {
                        if (_map.CityBlocks[x, y, z].SlopeType != SlopeType.None && _map.CityBlocks[x, y, z].SlopeType != SlopeType.SlopeAbove)
                            blocks[x, y, z] = CollisionMapType.Slope;
                    }
                }

            }

            for (var z = _map.Height - 1; z >= 0; z--)
            {
                for (var x = 0; x < _map.Width; x++)
                {
                    for (var y = 0; y < _map.Length; y++)
                    {
                        //remove Unknown blocks
                        if (blocks[x, y, z] == CollisionMapType.Unknwon)
                            blocks[x, y, z] = CollisionMapType.Block;
                        //ToDo, well, Unchecked (None) blocks could actually be possible, if you fall from a block above, but I don't think that happens in the original maps...
                        //So let's mark them 'Block'
                        if (blocks[x, y, z] == CollisionMapType.None)
                            //blocks = FloodFill(new Vector2(x, y), true);
                            blocks[x, y, z] = CollisionMapType.Block;
                    }
                }
            }
        }

        #region Flood Fill (--> Free)

        public CollisionMapType[,,] FloodFill(Vector2 start)
        {
            return FloodFill(start, CollisionMapType.Free);
        }

        private CollisionMapType[,,] FloodFill(Vector2 start, CollisionMapType typeToFill) //at the moment only <typeToFill == Free> works
        {
            var blocks = new CollisionMapType[_map.Width,_map.Length,_map.Height];
            for (var z = _map.Height - 1; z >= 0; z--)
            {
                var stack = new Stack<Vector2>();
                stack.Push(start);
                do
                {
                    var currentPos = stack.Pop();
                    var currentBlock = _map.CityBlocks[(int) currentPos.X, (int) currentPos.Y, z];
                    if (CheckBlockBounds(currentPos))
                    {
                        if (currentBlock.IsEmpty)
                            blocks[(int) currentPos.X, (int) currentPos.Y, z] = typeToFill;
                        else
                            blocks[(int) currentPos.X, (int) currentPos.Y, z] = CollisionMapType.Unknwon;
                    }

                    var newPos = new Vector2(currentPos.X + 1, currentPos.Y); //right
                    if (CheckBlockBounds(newPos))
                    {
                        if (CheckNeighbor((int) newPos.X, (int) newPos.Y, z, blocks, BlockFaceDirection.Left, typeToFill))
                            stack.Push(newPos);
                    }
                    newPos = new Vector2(currentPos.X, currentPos.Y + 1); //bottom
                    if (CheckBlockBounds(newPos))
                    {
                        if (CheckNeighbor((int) newPos.X, (int) newPos.Y, z, blocks, BlockFaceDirection.Top, typeToFill))
                            stack.Push(newPos);
                    }
                    newPos = new Vector2(currentPos.X - 1, currentPos.Y); //left
                    if (CheckBlockBounds(newPos))
                    {
                        if (CheckNeighbor((int) newPos.X, (int) newPos.Y, z, blocks, BlockFaceDirection.Right,
                                          typeToFill))
                            stack.Push(newPos);
                    }
                    newPos = new Vector2(currentPos.X, currentPos.Y - 1); //top
                    if (CheckBlockBounds(newPos))
                    {
                        if (CheckNeighbor((int) newPos.X, (int) newPos.Y, z, blocks, BlockFaceDirection.Bottom,
                                          typeToFill))
                            stack.Push(newPos);
                    }
                } while (stack.Count > 0);
            }
            return blocks;
        }

        private bool CheckNeighbor(int x, int y, int z, CollisionMapType[,,] blocks, BlockFaceDirection direction, CollisionMapType typeToFill)
        {
            if (blocks[x, y,z] == CollisionMapType.None)
            {
                var newBlock = _map.CityBlocks[x, y, z];
                if (newBlock.IsEmpty)
                    blocks[x, y, z] = typeToFill;
                if (newBlock.SlopeType != SlopeType.None && newBlock.SlopeType != SlopeType.SlopeAbove)
                {
                    blocks[x, y,z] = CollisionMapType.Slope;
                    return false;
                }
                switch (direction)
                {
                    case BlockFaceDirection.Left:
                        if (!newBlock.Left.Wall)
                            return true;
                        break;
                    case BlockFaceDirection.Right:
                        if (!newBlock.Right.Wall)
                            return true;
                        break;
                    case BlockFaceDirection.Top:
                        if (!newBlock.Top.Wall)
                            return true;
                        break;
                    case BlockFaceDirection.Bottom:
                        if (!newBlock.Bottom.Wall)
                            return true;
                        break;
                }
            }
            else if (blocks[x, y,z] == CollisionMapType.Unknwon)
            {
                blocks[x, y,z] = UnknownBlocks(x, y, z, typeToFill);
            }
            return false;
        }

        private CollisionMapType UnknownBlocks(int x, int y, int z, CollisionMapType typeToFill)
        {
            var newBlock = _map.CityBlocks[x, y, z];
            if (newBlock.Left)
            {
                if (CheckBlockBounds(new Vector2(x - 1, y)))
                {
                    if (_map.CityBlocks[x - 1, y, z].Right)
                        return typeToFill;
                }
            }
            if (newBlock.Top)
            {
                if (CheckBlockBounds(new Vector2(x, y - 1)))
                {
                    if (_map.CityBlocks[x, y - 1, z].Bottom)
                        return typeToFill;
                }
            }
            if (newBlock.Right)
            {
                if (CheckBlockBounds(new Vector2(x + 1, y)))
                {
                    if (_map.CityBlocks[x + 1, y, z].Left)
                        return typeToFill;
                }
            }
            if (newBlock.Bottom)
            {
                if (CheckBlockBounds(new Vector2(x, y + 1)))
                {
                    if (_map.CityBlocks[x, y + 1, z].Top)
                        return typeToFill;
                }
            }
            return typeToFill == CollisionMapType.Free ? CollisionMapType.Block : CollisionMapType.Free; //this method only works well with typeToFill = Free
        }

        #endregion

        #region Line

        private void FindLineObstacles(CollisionMapType[, ,] blocks, List<IObstacle> obstacles)
        {
            var stack = new Stack<Vector2>();

            //we check all 'Blocked blocks' which are 1 block wide, maybe they are not all blocked, but only a line is blocked for example a fence.
            for (var z = _map.Height - 1; z >= 0; z--)
            {
                var rawLineObstacles = new List<LineObstacle>();
                for (var x = 0; x < _map.Width; x++)
                {
                    for (var y = 0; y < _map.Length; y++)
                    {
                        if (blocks[x, y, z] != CollisionMapType.Block)
                            continue;
                        if (_map.CityBlocks[x, y, z].Left && !_map.CityBlocks[x, y, z].Right) //left
                        {
                            stack.Push(new Vector2(x, y)); //X
                        }
                        if (_map.CityBlocks[x, y, z].Right && !_map.CityBlocks[x, y, z].Left) //right
                        {
                            if ((x - 1) >= 0 && blocks[x - 1, y, z] == CollisionMapType.Free)
                            {
                                if (!_map.CityBlocks[x, y, z].Top && !_map.CityBlocks[x, y, z].Bottom)
                                    blocks[x, y, z] = CollisionMapType.Free;
                                rawLineObstacles.Add(new LineObstacle(new Vector2(x + 1, y), new Vector2(x + 1, y + 1), z, LineObstacleType.Vertical));
                            }
                        }

                        if (_map.CityBlocks[x, y, z].Top && !_map.CityBlocks[x, y, z].Bottom) //top
                        {
                            stack.Push(new Vector2(x, y));
                        }

                        if (_map.CityBlocks[x, y, z].Bottom && !_map.CityBlocks[x, y, z].Top) //bottom
                        {
                            if ((y - 1) >= 0 && blocks[x, y - 1, z] == CollisionMapType.Free)
                            {
                                blocks[x, y, z] = CollisionMapType.Free;
                                rawLineObstacles.Add(new LineObstacle(new Vector2(x, y + 1), new Vector2(x + 1, y + 1), z, LineObstacleType.Horizontal));
                            }
                        }
                    }
                    while (stack.Count > 0)
                    {
                        var vector2 = stack.Pop();
                        var y = (int)vector2.Y;
                        if (_map.CityBlocks[x, y, z].Left && !_map.CityBlocks[x, y, z].Right) //left
                        {
                            if ((x + 1) < _map.Width && blocks[x + 1, y, z] == CollisionMapType.Free)
                            {
                                blocks[x, y, z] = CollisionMapType.Free;
                                rawLineObstacles.Add(new LineObstacle(new Vector2(x, y), new Vector2(x, y + 1), z, LineObstacleType.Vertical));
                            }
                        }
                        if (_map.CityBlocks[x, y, z].Top && !_map.CityBlocks[x, y, z].Bottom) //top
                        {
                            if ((y + 1) < _map.Length && blocks[x, y + 1, z] == CollisionMapType.Free)
                            {
                                if (!_map.CityBlocks[x, y, z].Left && !_map.CityBlocks[x, y, z].Right)
                                    blocks[x, y, z] = CollisionMapType.Free;
                                rawLineObstacles.Add(new LineObstacle(new Vector2(x, y), new Vector2(x + 1, y), z, LineObstacleType.Horizontal));
                            }
                        }
                    }
                }

                //find single "blocked" blocks
                for (var x = 0; x < _map.Width; x++)
                {
                    for (var y = 0; y < _map.Length; y++)
                    {
                        if (blocks[x, y, z] != CollisionMapType.Block)
                            continue;
                        if (x - 1 >= 0 && blocks[x - 1, y, z] != CollisionMapType.Block && //Left
                            x + 1 < _map.Width && blocks[x + 1, y, z] != CollisionMapType.Block && //Right
                            y - 1 >= 0 && blocks[x, y - 1, z] != CollisionMapType.Block && //Top
                            y + 1 < _map.Length && blocks[x, y + 1, z] != CollisionMapType.Block) //Bottom
                        {
                            if (!_map.CityBlocks[x, y, z].Left || !_map.CityBlocks[x, y, z].Right || !_map.CityBlocks[x, y, z].Top || !_map.CityBlocks[x, y, z].Bottom)
                            {
                                if (_map.CityBlocks[x, y, z].Left.Wall)
                                    rawLineObstacles.Add(new LineObstacle(new Vector2(x, y), new Vector2(x, y + 1), z, LineObstacleType.Vertical));
                                if (_map.CityBlocks[x, y, z].Right.Wall)
                                    rawLineObstacles.Add(new LineObstacle(new Vector2(x + 1, y), new Vector2(x + 1, y + 1), z, LineObstacleType.Vertical));
                                if (_map.CityBlocks[x, y, z].Top.Wall)
                                    rawLineObstacles.Add(new LineObstacle(new Vector2(x, y), new Vector2(x + 1, y), z, LineObstacleType.Horizontal));
                                if (_map.CityBlocks[x, y, z].Bottom.Wall)
                                    rawLineObstacles.Add(new LineObstacle(new Vector2(x, y + 1), new Vector2(x + 1, y + 1), z, LineObstacleType.Horizontal));
                                blocks[x, y, z] = CollisionMapType.Free;
                            }
                        }
                    }
                }
                var lineObstacles = OptimizeStraightVertices(rawLineObstacles, z);
                obstacles.AddRange(lineObstacles);
            }
        }

        /// <summary> 
        /// Combines straight obstacles to optimize collision detection.
        /// </summary>
        private IEnumerable<IObstacle> OptimizeStraightVertices(IEnumerable<LineObstacle> straightObstacles, int z)
        {
            var lineObstacles = new List<IObstacle>();
            var obstaclesHorizontal = new bool[256,256 + 1];
            var obstaclesVertical = new bool[256 + 1,256];
            foreach (var straightObstacle in straightObstacles)
            {
                var lineObstacle = straightObstacle;
                if (lineObstacle.Type == LineObstacleType.Horizontal)
                    obstaclesHorizontal[(int) lineObstacle.Start.X, (int) lineObstacle.Start.Y] = true;
                else if (lineObstacle.Type == LineObstacleType.Vertical)
                    obstaclesVertical[(int) lineObstacle.Start.X, (int) lineObstacle.Start.Y] = true;
            }

            //Horizontal
            for (var y = 0; y < obstaclesHorizontal.GetLength(1); y++)
            {
                var start = new Vector2();
                var open = false;
                for (var x = 0; x < obstaclesHorizontal.GetLength(0); x++)
                {
                    if (!obstaclesHorizontal[x, y])
                    {
                        if (open)
                        {
                            var end = new Vector2(x, y);
                            lineObstacles.Add(new LineObstacle(start, end, z, LineObstacleType.Horizontal));
                            open = false;
                        }
                        continue;
                    }
                    if (open)
                        continue;
                    open = true;
                    start = new Vector2(x, y);
                }
            }

            for (var x = 0; x < obstaclesVertical.GetLength(0); x++)
            {
                var start = new Vector2();
                var open = false;
                for (var y = 0; y < obstaclesVertical.GetLength(1); y++)
                {
                    if (!obstaclesVertical[x, y])
                    {
                        if (open)
                        {
                            var end = new Vector2(x, y);
                            lineObstacles.Add(new LineObstacle(start, end, z, LineObstacleType.Vertical));
                            open = false;
                        }
                        continue;
                    }
                    if (open)
                        continue;
                    open = true;
                    start = new Vector2(x, y);
                }
            }
            return lineObstacles;
        }

        #endregion
    }
}