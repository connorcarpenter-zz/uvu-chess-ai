using System;
using System.Collections.Generic;
using System.Linq;
using UvsChess;

namespace StudentAI
{
    public class StudentAI : IChessAI
    {
        const int pawnValue = 100;
        const int knightValue = 320;
        const int bishopValue = 333;
        const int rookValue = 510;
        const int queenValue = 880;
        const int kingValue = 10000;
        const int openMoveValue = 5;
        const int checkValue = 500;
        const int checkmateValue = 10000;
        const int randomValue = 50;

        //lower is higher here
        private const int baselinePawnRankModifier = 20;
        private const int dontDrawPawnRankModifier = 1;
        public static int PawnRankModifier = baselinePawnRankModifier;
        const int distKingModifier = 60;

        public int HalfMoves = 0;
        public string Name
        {
            get { return "ConnorAI (debug)"; }
        }

        private enum AIProfilerTags
        {
            GetNextMove,
            IsValidMove
        }

        /// Evaluates the chess board and decided which move to make. This is the main method of the AI.
        public ChessMove GetNextMove(ChessBoard board, ChessColor myColor)
        {
            #region profiler
            //For more information about using the profiler, visit http://code.google.com/p/uvschess/wiki/ProfilerHowTo
            // This is how you setup the profiler. This should be done in GetNextMove.
            Profiler.TagNames = Enum.GetNames(typeof(AIProfilerTags));

            // In order for the profiler to calculate the AI's nodes/sec value,
            // I need to tell the profiler which method key's are for MiniMax.
            // In this case our mini and max methods are the same,
            // but usually they are not.
            Profiler.MinisProfilerTag = (int)AIProfilerTags.GetNextMove;
            Profiler.MaxsProfilerTag = (int)AIProfilerTags.GetNextMove;

            // This increments the method call count by 1 for GetNextMove in the profiler
            Profiler.IncrementTagCount((int)AIProfilerTags.GetNextMove);
            #endregion

            PawnRankModifier = HalfMoves > 30 ? dontDrawPawnRankModifier : baselinePawnRankModifier;

            ChessMove myNextMove = null;
            var allMoves = GetMyMoves(board, myColor);
            allMoves = EvaluateMoves(allMoves);
            allMoves.Sort();
            allMoves.Reverse();

            //while (!IsMyTurnOver())
            //{
            //    if (myNextMove != null) continue;

            myNextMove = allMoves.First().Move;

                #region Logging
                this.Log("value of move: "+allMoves.First().Move.ValueOfMove);
                this.Log("half moves: " + HalfMoves);
                this.Log(string.Empty);
                #endregion

            //    break;
            //}

            #region Profiler
            Profiler.SetDepthReachedDuringThisTurn(2);
            #endregion

            HalfMoves++;
            var currentPiece = board[myNextMove.From.X, myNextMove.From.Y];
            if (currentPiece == ChessPiece.BlackPawn || currentPiece == ChessPiece.WhitePawn)
                HalfMoves = 0;

            return myNextMove;
        }

        /// Validates a move. The framework uses this to validate the opponents move.
        public bool IsValidMove(ChessBoard boardBeforeMove, ChessMove moveToCheck, ChessColor colorOfPlayerMoving)
        {
            #region Profiler
            Profiler.IncrementTagCount((int)AIProfilerTags.IsValidMove);
            #endregion

            HalfMoves++;
            var currentPiece = boardBeforeMove[moveToCheck.From.X, moveToCheck.From.Y];
            if (currentPiece == ChessPiece.BlackPawn || currentPiece == ChessPiece.WhitePawn)
                HalfMoves = 0;

            var allMoves = GetMyMoves(boardBeforeMove,colorOfPlayerMoving);
            allMoves = EvaluateMoves(allMoves);
            return IsMovingRightColorPiece(boardBeforeMove, moveToCheck, colorOfPlayerMoving) && MoveDataListContains(allMoves, moveToCheck);
        }

        private void SetMoveFlagAndValue(ChessMoveData moveData)
        {
            var myMoves = StudentAI.GetMyMoves(moveData.NewBoard, moveData.Color);
            
            //see if checking other player
            bool any = false;
            foreach (var move in myMoves)
            {
                var oldToTile = move.OldBoard[move.Move.To.X, move.Move.To.Y];
                if ((moveData.Color == ChessColor.Black && oldToTile == ChessPiece.WhiteKing) || (moveData.Color == ChessColor.White && oldToTile == ChessPiece.BlackKing))
                {
                    any = true;
                    break;
                }
            }
            moveData.Move.Flag = any
                ? ChessFlag.Check : ChessFlag.NoFlag;

            //see if in checkmate
            if (moveData.Move.Flag == ChessFlag.Check)
                if(InCheckmate(moveData))
                    moveData.Move.Flag = ChessFlag.Checkmate;

            moveData.Move.ValueOfMove = GetBoardValue(moveData.NewBoard, moveData.Color);

            var otherMoves = StudentAI.GetMyMoves(moveData.NewBoard, EnemyColor(moveData.Color));
            foreach (var otherMove in otherMoves)
            {
                if (otherMove.Move.To.X != moveData.Move.To.X || otherMove.Move.To.Y != moveData.Move.To.Y)
                    continue;

                var currentPiece = moveData.NewBoard[moveData.Move.To.X, moveData.Move.To.Y];
                if (currentPiece == ChessPiece.Empty) continue;
                var colorMulti = (GetChessPieceColor(currentPiece) == moveData.Color) ? 1 : -1;
                var value = 0;
                switch (currentPiece)
                {
                    case ChessPiece.BlackPawn:
                        value += colorMulti * pawnValue;
                        value += colorMulti * (pawnValue / PawnRankModifier) * (moveData.Move.To.Y - 1);
                        break;

                    case ChessPiece.WhitePawn:
                        value += colorMulti * pawnValue;
                        value += colorMulti * (pawnValue / PawnRankModifier) * (6 - moveData.Move.To.Y);
                        break;

                    case ChessPiece.BlackRook:
                    case ChessPiece.WhiteRook:
                        value += colorMulti * rookValue;
                        break;

                    case ChessPiece.BlackKnight:
                    case ChessPiece.WhiteKnight:
                        value += colorMulti * knightValue;
                        break;

                    case ChessPiece.BlackBishop:
                    case ChessPiece.WhiteBishop:
                        value += colorMulti * bishopValue;
                        break;

                    case ChessPiece.BlackQueen:
                    case ChessPiece.WhiteQueen:
                        value += colorMulti * queenValue;
                        break;

                    case ChessPiece.BlackKing:
                    case ChessPiece.WhiteKing:
                        break;

                    default:
                        throw new Exception("Somehow not catching a specific piece? Empty?");
                        break;
                }
                value = (int)(value*1.5f);
                moveData.Move.ValueOfMove -= value;
                break;
            }

            var random = new Random();
            moveData.Move.ValueOfMove += random.Next(randomValue*2) - randomValue;

            if (moveData.Move.Flag == ChessFlag.Check)
                moveData.Move.ValueOfMove += checkValue;
            if (moveData.Move.Flag == ChessFlag.Checkmate)
                moveData.Move.ValueOfMove += checkmateValue;
        }

        private static bool InCheckmate(ChessMoveData moveData)
        {
            //get all enemy moves
            var enemyMoves = GetMyMoves(moveData.NewBoard, EnemyColor(moveData.Color));

            //if any enemy move results in a board where i can't take the king, is not checkmate
            var checkmate = true;

            foreach (var enemyMove in enemyMoves)
            {
                var myCounterMoves = GetMyMoves(enemyMove.NewBoard, moveData.Color);
                var canTakeKing = false;
                foreach (var counterMove in myCounterMoves)
                {
                    var toTile = counterMove.OldBoard[counterMove.Move.To.X, counterMove.Move.To.Y];
                    if ((moveData.Color == ChessColor.Black && toTile == ChessPiece.WhiteKing) ||
                        (moveData.Color == ChessColor.White && toTile == ChessPiece.BlackKing))
                    {
                        canTakeKing = true;
                        break;
                    }
                }
                if (!canTakeKing)
                {
                    checkmate = false;
                    break;
                }
            }

            return checkmate;
        }

        private static ChessLocation GetPiecePosition(ChessBoard board, ChessPiece piece)
        {
            for(var i=0;i<8;i++)
                for (var j = 0; j < 8; j++)
                {
                    if(board[i,j]==piece)
                        return new ChessLocation(i,j);    
                }
            return null;
        }

        private bool MoveDataListContains(List<ChessMoveData> chessMoveData, ChessMove moveToCheck)
        {
            return chessMoveData.Any(m => m.Move == moveToCheck);
        }

        public static List<ChessMoveData> GetMyMoves(ChessBoard board, ChessColor myColor)
        {
            return GetAllMoves(board,false,myColor);
        } 
        private static List<ChessMoveData> GetAllMoves(ChessBoard board, bool AllColorsAllowed = true, ChessColor onlyThisColor = ChessColor.Black)
        {
            var moveList = new List<ChessMoveData>();

            for (var y = 0; y < ChessBoard.NumberOfRows; y++)
            {
                for (var x = 0; x < ChessBoard.NumberOfColumns; x++)
                {
                    var currentPiece = board[x, y];
                    if (currentPiece == ChessPiece.Empty) continue;
                    if (!AllColorsAllowed && GetChessPieceColor(currentPiece) != onlyThisColor) continue;
                    switch (currentPiece)
                    {
                        case ChessPiece.BlackPawn:
                        case ChessPiece.WhitePawn:
                            AddPawnMoves(moveList, board, x, y, GetChessPieceColor(currentPiece));
                            break;

                        case ChessPiece.BlackRook:
                        case ChessPiece.WhiteRook:
                            AddRookMoves(moveList, board, x, y, GetChessPieceColor(currentPiece));
                            break;

                        case ChessPiece.BlackKnight:
                        case ChessPiece.WhiteKnight:
                            AddKnightMoves(moveList, board, x, y, GetChessPieceColor(currentPiece));
                            break;

                        case ChessPiece.BlackBishop:
                        case ChessPiece.WhiteBishop:
                            AddBishopMoves(moveList, board, x, y, GetChessPieceColor(currentPiece));
                            break;

                        case ChessPiece.BlackQueen:
                        case ChessPiece.WhiteQueen:
                            AddQueenMoves(moveList, board, x, y, GetChessPieceColor(currentPiece));
                            break;

                        case ChessPiece.BlackKing:
                        case ChessPiece.WhiteKing:
                            AddKingMoves(moveList, board, x, y, GetChessPieceColor(currentPiece));
                            break;

                        default:
                            throw new Exception("Somehow not catching a specific piece? Empty?");
                            break;
                    }
                }
            }

            return moveList;
        }

        public class ChessMoveData:IComparable<ChessMoveData>
        {
            public ChessMove Move { get; set; }
            public ChessBoard OldBoard { get; set; }
            public ChessBoard NewBoard { get; set; }
            public ChessColor Color { get; set; }

            public ChessMoveData(ChessBoard board, ChessMove move, ChessColor color)
            {
                OldBoard = board;
                Move = move;
                Color = color;

                NewBoard = board.Clone();
                NewBoard.MakeMove(move);
            }

            public int CompareTo(ChessMoveData other)
            {
                return Move.CompareTo(other.Move);
            }
        }

        private static ChessMoveData NewChessMove(ChessBoard board, ChessLocation from, ChessLocation to, ChessColor color)
        {
            var move = new ChessMove(from, to);
            return new ChessMoveData(board, move, color);
        }

        private static void AddPawnMoves(List<ChessMoveData> moveList, ChessBoard board, int x, int y, ChessColor pieceColor)
        {
            var startLocation = new ChessLocation(x, y);
            var canJump = (pieceColor==ChessColor.Black && y==1) || (pieceColor==ChessColor.White && y==6);
            var yForward = (pieceColor == ChessColor.Black) ? 1 : -1;

            if (TileIsValid(x, y + yForward) && TileIsFree(board, x, y + yForward))
            {
                moveList.Add(NewChessMove(board, startLocation, new ChessLocation(x, y + yForward), pieceColor));
                if (canJump)
                    if (TileIsValid(x, y + yForward * 2) && TileIsFree(board, x, y + yForward * 2))
                        moveList.Add(NewChessMove(board, startLocation, new ChessLocation(x, y + yForward * 2), pieceColor));
            }
            
            if (TileIsValid(x + 1, y + yForward) && TileHasColor(board, x + 1, y + yForward, EnemyColor(pieceColor)))
                moveList.Add(NewChessMove(board, startLocation, new ChessLocation(x + 1, y + yForward), pieceColor));

            if (TileIsValid(x - 1, y + yForward) && TileHasColor(board, x - 1, y + yForward, EnemyColor(pieceColor)))
                moveList.Add(NewChessMove(board, startLocation, new ChessLocation(x - 1, y + yForward), pieceColor));
        }

        private static void AddRookMoves(List<ChessMoveData> moveList, ChessBoard board, int x, int y, ChessColor pieceColor)
        {
            AddRiderMoves(moveList, board, x, y, pieceColor, 1, 0);
            AddRiderMoves(moveList, board, x, y, pieceColor, -1, 0);
            AddRiderMoves(moveList, board, x, y, pieceColor, 0, 1);
            AddRiderMoves(moveList, board, x, y, pieceColor, 0, -1);
        }

        private static void AddKnightMoves(List<ChessMoveData> moveList, ChessBoard board, int x, int y, ChessColor pieceColor)
        {
            AddLeaperMoves(moveList, board, x, y, pieceColor, 1, 2);
            AddLeaperMoves(moveList, board, x, y, pieceColor, 1, -2);
            AddLeaperMoves(moveList, board, x, y, pieceColor, 2, 1);
            AddLeaperMoves(moveList, board, x, y, pieceColor, 2, -1);
            AddLeaperMoves(moveList, board, x, y, pieceColor, -1, 2);
            AddLeaperMoves(moveList, board, x, y, pieceColor, -1, -2);
            AddLeaperMoves(moveList, board, x, y, pieceColor, -2, 1);
            AddLeaperMoves(moveList, board, x, y, pieceColor, -2, -1);
        }

        private static void AddBishopMoves(List<ChessMoveData> moveList, ChessBoard board, int x, int y, ChessColor pieceColor)
        {
            AddRiderMoves(moveList, board, x, y, pieceColor, 1, 1);
            AddRiderMoves(moveList, board, x, y, pieceColor, -1, -1);
            AddRiderMoves(moveList, board, x, y, pieceColor, -1, 1);
            AddRiderMoves(moveList, board, x, y, pieceColor, 1, -1);
        }

        private static void AddQueenMoves(List<ChessMoveData> moveList, ChessBoard board, int x, int y, ChessColor pieceColor)
        {
            AddRiderMoves(moveList, board, x, y, pieceColor, 1, 0);
            AddRiderMoves(moveList, board, x, y, pieceColor, -1, 0);
            AddRiderMoves(moveList, board, x, y, pieceColor, 0, 1);
            AddRiderMoves(moveList, board, x, y, pieceColor, 0, -1);
            AddRiderMoves(moveList, board, x, y, pieceColor, 1, 1);
            AddRiderMoves(moveList, board, x, y, pieceColor, -1, -1);
            AddRiderMoves(moveList, board, x, y, pieceColor, -1, 1);
            AddRiderMoves(moveList, board, x, y, pieceColor, 1, -1);
        }

        private static void AddRiderMoves(List<ChessMoveData> moveList, ChessBoard board, int x, int y, ChessColor pieceColor, int modX, int modY)
        {
            var i = 1;
            while (true)
            {
                var newX = x + modX * i;
                var newY = y + modY * i;
                if (TileIsValid(newX, newY))
                {
                    if (TileHasColor(board, newX, newY, EnemyColor(pieceColor)))
                    {
                        moveList.Add(NewChessMove(board, new ChessLocation(x, y), new ChessLocation(newX, newY), pieceColor));
                        break;
                    }
                    if (TileIsFree(board, newX, newY))
                    {
                        moveList.Add(NewChessMove(board, new ChessLocation(x, y), new ChessLocation(newX, newY), pieceColor));
                        i++;
                        continue;
                    }
                    else
                    {
                        break;
                    }
                }
                else
                {
                    break;
                }
            }
        }

        private static void AddKingMoves(List<ChessMoveData> moveList, ChessBoard board, int x, int y, ChessColor pieceColor)
        {
            AddLeaperMoves(moveList, board, x, y, pieceColor, 0, 1);
            AddLeaperMoves(moveList, board, x, y, pieceColor, 0, -1);
            AddLeaperMoves(moveList, board, x, y, pieceColor, 1, 0);
            AddLeaperMoves(moveList, board, x, y, pieceColor, -1, 0);
            AddLeaperMoves(moveList, board, x, y, pieceColor, 1, 1);
            AddLeaperMoves(moveList, board, x, y, pieceColor, 1, -1);
            AddLeaperMoves(moveList, board, x, y, pieceColor, -1, 1);
            AddLeaperMoves(moveList, board, x, y, pieceColor, -1, -1);
        }

        private static void AddLeaperMoves(List<ChessMoveData> moveList, ChessBoard board, int x, int y,
            ChessColor pieceColor, int modX, int modY)
        {
            var newX = x + modX;
            var newY = y + modY;
            if (TileIsValid(newX, newY) && (TileIsFree(board, newX, newY) || TileHasColor(board, newX, newY, EnemyColor(pieceColor))))
                moveList.Add(NewChessMove(board, new ChessLocation(x, y), new ChessLocation(newX, newY), pieceColor));
        }

        private static bool TileIsValid(int x, int y)
        {
            return !(x < 0 || y < 0 || x > 7 || y > 7);
        }

        private static ChessColor GetChessPieceColor(ChessPiece piece)
        {
            if (piece < ChessPiece.Empty) return ChessColor.Black;
            if (piece > ChessPiece.Empty) return ChessColor.White;
            throw new Exception("You didn't check whether this was an empty tile before calling this method!");
        }

        private static ChessColor EnemyColor(ChessColor color)
        {
            return color == ChessColor.Black ? ChessColor.White : ChessColor.Black;
        }

        private static bool TileHasColor(ChessBoard board, int x, int y, ChessColor color)
        {
            var piece = board[x, y];
            if (piece == ChessPiece.Empty) return false;
            return (color == GetChessPieceColor(piece));
        }

        private static bool TileIsFree(ChessBoard board, int x, int y)
        {
            return board[x, y] == ChessPiece.Empty;
        }

        ///TODO: seperate flags adding and value adding
        /// value adding shouldn't happen when checking if IsValid
        private List<ChessMoveData> EvaluateMoves(List<ChessMoveData> moveList)
        {
            var newList = new List<ChessMoveData>();
            
            //remove moves that lead to check
            foreach (var move in moveList)
            {
                var isInCheck = false;
                var enemyMoves = GetMyMoves(move.NewBoard, EnemyColor(move.Color));
                foreach (var enemyMove in enemyMoves)
                {
                    var toTile = enemyMove.OldBoard[enemyMove.Move.To.X, enemyMove.Move.To.Y];
                    if ((enemyMove.Color == ChessColor.Black && toTile == ChessPiece.WhiteKing) ||
                        (enemyMove.Color == ChessColor.White && toTile == ChessPiece.BlackKing))
                    {
                        isInCheck = true;
                        break;
                    }
                }
                if (!isInCheck)
                {
                    newList.Add(move);
                    continue;
                }
            }
            moveList = newList;

            //get value of move
            foreach (var move in moveList)
            {
                SetMoveFlagAndValue(move);
            }

            //move pawns if half moves is too high (avoid stalemate)
            if (HalfMoves > 30)
            {
                foreach (var move in moveList)
                {
                    var currentPiece = move.OldBoard[move.Move.From.X, move.Move.From.Y];
                    if (currentPiece != ChessPiece.BlackPawn && currentPiece != ChessPiece.WhitePawn)
                        continue;
                    move.Move.ValueOfMove += 1000;
                }
            }

            return moveList;
        }

        private static int GetBoardValue(ChessBoard board, ChessColor myColor)
        {
            var whiteKingLocation = GetPiecePosition(board, ChessPiece.WhiteKing);
            var blackKingLocation = GetPiecePosition(board, ChessPiece.BlackKing);

            var value = 0;
            for (var y = 0; y < ChessBoard.NumberOfRows; y++)
            {
                for (var x = 0; x < ChessBoard.NumberOfColumns; x++)
                {
                    var currentPiece = board[x, y];
                    if (currentPiece == ChessPiece.Empty) continue;
                    var colorMulti = (GetChessPieceColor(currentPiece) == myColor) ? 1 : -1;
                    int distToKing;
                    var kingLocation = (GetChessPieceColor(currentPiece) == ChessColor.Black)
                        ? whiteKingLocation
                        : blackKingLocation;
                    if (kingLocation == null)
                    {
                        distToKing = 10;
                    }
                    else
                    {
                        var dis = Convert.ToInt32(Math.Sqrt(Math.Pow(x - kingLocation.X, 2) + Math.Pow(y - kingLocation.Y, 2)));
                        if (dis < 2) dis = 10;
                        distToKing = 10 - dis;
                    }

                    switch (currentPiece)
                    {
                        case ChessPiece.BlackPawn:
                            value += colorMulti*pawnValue;
                            value += colorMulti*(pawnValue/PawnRankModifier)*(y - 1);
                            break;

                        case ChessPiece.WhitePawn:
                            value += colorMulti * pawnValue;
                            value += colorMulti * (pawnValue / PawnRankModifier) * (6 - y);
                            break;

                        case ChessPiece.BlackRook:
                        case ChessPiece.WhiteRook:
                            value += colorMulti * rookValue;
                            value += colorMulti*(rookValue/distKingModifier)*distToKing;
                            break;

                        case ChessPiece.BlackKnight:
                        case ChessPiece.WhiteKnight:
                            value += colorMulti * knightValue;
                            value += colorMulti * (knightValue / distKingModifier) * distToKing;
                            break;

                        case ChessPiece.BlackBishop:
                        case ChessPiece.WhiteBishop:
                            value += colorMulti * bishopValue;
                            value += colorMulti * (bishopValue / distKingModifier) * distToKing;
                            break;

                        case ChessPiece.BlackQueen:
                        case ChessPiece.WhiteQueen:
                            value += colorMulti * queenValue;
                            value += colorMulti * (queenValue / distKingModifier) * distToKing;
                            break;

                        case ChessPiece.BlackKing:
                        case ChessPiece.WhiteKing:
                            value += colorMulti * kingValue;
                            break;

                        default:
                            throw new Exception("Somehow not catching a specific piece? Empty?");
                            break;
                    }
                }
            }

            return value;
        }

        private static bool IsMovingRightColorPiece(ChessBoard board, ChessMove moveToCheck, ChessColor colorOfPlayerMoving)
        {
            return (GetChessPieceColor(board[moveToCheck.From.X, moveToCheck.From.Y]) == colorOfPlayerMoving);
        }

        #region IChessAI Members that should be implemented as automatic properties and should NEVER be touched by students.
        /// <summary>
        /// This will return false when the framework starts running your AI. When the AI's time has run out,
        /// then this method will return true. Once this method returns true, your AI should return a 
        /// move immediately.
        /// 
        /// You should NEVER EVER set this property!
        /// This property should be defined as an Automatic Property.
        /// This property SHOULD NOT CONTAIN ANY CODE!!!
        /// </summary>
        public AIIsMyTurnOverCallback IsMyTurnOver { get; set; }

        /// <summary>
        /// Call this method to print out debug information. The framework subscribes to this event
        /// and will provide a log window for your debug messages.
        /// 
        /// You should NEVER EVER set this property!
        /// This property should be defined as an Automatic Property.
        /// This property SHOULD NOT CONTAIN ANY CODE!!!
        /// </summary>
        /// <param name="message"></param>
        public AILoggerCallback Log { get; set; }

        /// <summary>
        /// Call this method to catch profiling information. The framework subscribes to this event
        /// and will print out the profiling stats in your log window.
        /// 
        /// You should NEVER EVER set this property!
        /// This property should be defined as an Automatic Property.
        /// This property SHOULD NOT CONTAIN ANY CODE!!!
        /// </summary>
        /// <param name="key"></param>
        public AIProfiler Profiler { get; set; }

        /// <summary>
        /// Call this method to tell the framework what decision print out debug information. The framework subscribes to this event
        /// and will provide a debug window for your decision tree.
        /// 
        /// You should NEVER EVER set this property!
        /// This property should be defined as an Automatic Property.
        /// This property SHOULD NOT CONTAIN ANY CODE!!!
        /// </summary>
        /// <param name="message"></param>
        public AISetDecisionTreeCallback SetDecisionTree { get; set; }
        #endregion
    }
}
