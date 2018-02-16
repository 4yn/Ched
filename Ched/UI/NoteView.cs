﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Reactive.Linq;
using System.Reactive.Disposables;

using Ched.Components;
using Ched.Components.Notes;
using Ched.UI.Operations;

namespace Ched.UI
{
    public partial class NoteView : Control
    {
        public event EventHandler EditModeChanged;
        public event EventHandler NewNoteTypeChanged;
        public event EventHandler AirDirectionChanged;
        public event EventHandler OperationHistoryChanged;

        private Color barLineColor = Color.FromArgb(160, 160, 160);
        private Color beatLineColor = Color.FromArgb(80, 80, 80);
        private Color laneBorderLightColor = Color.FromArgb(60, 60, 60);
        private Color laneBorderDarkColor = Color.FromArgb(30, 30, 30);
        private int unitLaneWidth = 12;
        private int shortNoteHeight = 5;
        private float unitBeatHeight = 120;

        private bool editable = true;
        private EditMode editMode = EditMode.Edit;
        private NoteType newNoteType = NoteType.Tap;
        private AirDirection airDirection = new AirDirection(VerticalAirDirection.Up, HorizontalAirDirection.Center);
        private bool isNewSlideStepVisible = true;

        /// <summary>
        /// 小節の区切り線の色を設定します。
        /// </summary>
        public Color BarLineColor
        {
            get { return barLineColor; }
            set
            {
                barLineColor = value;
                Invalidate();
            }
        }

        /// <summary>
        /// 1拍のガイド線の色を設定します。
        /// </summary>
        public Color BeatLineColor
        {
            get { return beatLineColor; }
            set
            {
                beatLineColor = value;
                Invalidate();
            }
        }

        /// <summary>
        /// レーンのガイド線のメインカラーを設定します。
        /// </summary>
        public Color LaneBorderLightColor
        {
            get { return laneBorderLightColor; }
            set
            {
                laneBorderLightColor = value;
                Invalidate();
            }
        }

        /// <summary>
        /// レーンのガイド線のサブカラーを設定します。
        /// </summary>
        public Color LaneBorderDarkColor
        {
            get { return laneBorderDarkColor; }
            set
            {
                laneBorderDarkColor = value;
                Invalidate();
            }
        }

        /// <summary>
        /// 1レーンあたりの表示幅を設定します。
        /// </summary>
        public int UnitLaneWidth
        {
            get { return unitLaneWidth; }
            set
            {
                unitLaneWidth = value;
                Invalidate();
            }
        }

        /// <summary>
        /// レーンの表示幅を取得します。
        /// </summary>
        public int LaneWidth
        {
            get { return UnitLaneWidth * Constants.LanesCount + BorderThickness * (Constants.LanesCount - 1); }
        }

        /// <summary>
        /// レーンのガイド線の幅を取得します。
        /// </summary>
        public int BorderThickness { get { return (int)Math.Round(UnitLaneWidth * 0.1f); } }

        /// <summary>
        /// ショートノーツの表示高さを設定します。
        /// </summary>
        public int ShortNoteHeight
        {
            get { return shortNoteHeight; }
            set
            {
                shortNoteHeight = value;
                Invalidate();
            }
        }

        /// <summary>
        /// 1拍あたりのTick数を取得します。
        /// </summary>
        public int UnitBeatTick { get { return 480; } }

        /// <summary>
        /// 1拍あたりの表示高さを設定します。
        /// </summary>
        public float UnitBeatHeight
        {
            get { return unitBeatHeight; }
            set
            {
                // 6の倍数でいい感じに描画してくれる
                unitBeatHeight = value;
                Invalidate();
            }
        }

        /// <summary>
        /// クォンタイズを行うTick数を指定します。
        /// </summary>
        public int QuantizeTick { get; set; }

        /// <summary>
        /// 表示始端のTickを設定します。
        /// </summary>
        public int HeadTick { get; set; }

        /// <summary>
        /// 表示終端のTickを取得します。
        /// </summary>
        public int TailTick
        {
            get { return HeadTick + (int)(ClientSize.Height * UnitBeatTick / UnitBeatHeight); }
        }

        /// <summary>
        /// ノーツが編集可能かどうかを示す値を設定します。
        /// </summary>
        public bool Editable
        {
            get { return editable; }
            set
            {
                editable = value;
                Cursor = value ? Cursors.Default : Cursors.No;
            }
        }

        /// <summary>
        /// 編集モードを設定します。
        /// </summary>
        public EditMode EditMode
        {
            get { return editMode; }
            set
            {
                editMode = value;
                EditModeChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// 追加するノート種別を設定します。
        /// </summary>
        public NoteType NewNoteType
        {
            get { return newNoteType; }
            set
            {
                int bits = (int)value;
                bool isSingle = bits != 0 && (bits & (bits - 1)) == 0;
                if (!isSingle) throw new ArgumentException("value", "value must be single bit.");
                newNoteType = value;
                NewNoteTypeChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// 新たに追加するSlideのStepノートの可視性を設定します。
        /// </summary>
        public bool IsNewSlideStepVisible
        {
            get { return isNewSlideStepVisible; }
            set
            {
                isNewSlideStepVisible = value;
                NewNoteTypeChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// 追加するAIRの方向を設定します。
        /// </summary>
        public AirDirection AirDirection
        {
            get { return airDirection; }
            set
            {
                airDirection = value;
                AirDirectionChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        protected int LastWidth { get; set; } = 4;

        public bool CanUndo { get { return OperationManager.CanUndo; } }

        public bool CanRedo { get { return OperationManager.CanRedo; } }

        public NoteCollection Notes { get; } = new NoteCollection();

        protected OperationManager OperationManager { get; } = new OperationManager();

        protected CompositeDisposable Subscriptions { get; } = new CompositeDisposable();

        public NoteView()
        {
            InitializeComponent();
            this.DoubleBuffered = true;
            this.BackColor = Color.Black;
            this.SetStyle(ControlStyles.ResizeRedraw, true);
            this.SetStyle(ControlStyles.Opaque, true);

            OperationManager.OperationHistoryChanged += (s, e) =>
            {
                OperationHistoryChanged?.Invoke(this, EventArgs.Empty);
            };

            QuantizeTick = UnitBeatTick;

            var mouseDown = this.MouseDownAsObservable();
            var mouseMove = this.MouseMoveAsObservable();
            var mouseUp = this.MouseUpAsObservable();

            var editSubscription = mouseDown
                .Where(p => Editable)
                .Where(p => p.Button == MouseButtons.Left && EditMode == EditMode.Edit)
                .SelectMany(p =>
                {
                    int tailTick = TailTick;
                    var from = p.Location;
                    Matrix matrix = GetDrawingMatrix(new Matrix());
                    matrix.Invert();
                    PointF scorePos = matrix.TransformPoint(p.Location);

                    // そもそも描画領域外であれば何もしない
                    RectangleF scoreRect = new RectangleF(0, GetYPositionFromTick(HeadTick), LaneWidth, GetYPositionFromTick(TailTick) - GetYPositionFromTick(HeadTick));
                    if (!scoreRect.Contains(scorePos)) return Observable.Empty<MouseEventArgs>();

                    Func<AirAction.ActionNote, IObservable<MouseEventArgs>> actionNoteHandler = action =>
                    {
                        var offsets = new HashSet<int>(action.ParentNote.ActionNotes.Select(q => q.Offset));
                        offsets.Remove(action.Offset);
                        return mouseMove
                            .TakeUntil(mouseUp)
                            .Do(q =>
                            {
                                var currentScorePos = matrix.TransformPoint(q.Location);
                                int offset = GetQuantizedTick(GetTickFromYPosition(currentScorePos.Y)) - action.ParentNote.ParentNote.Tick;
                                if (offset <= 0 || offsets.Contains(offset)) return;
                                action.Offset = offset;
                                Cursor.Current = Cursors.SizeNS;
                            }).Finally(() => Cursor.Current = Cursors.Default);
                    };

                    // AIR-ACTION
                    foreach (var note in Notes.AirActions)
                    {
                        foreach (var action in note.ActionNotes)
                        {
                            RectangleF noteRect = GetClickableRectFromNotePosition(note.ParentNote.Tick + action.Offset, note.ParentNote.LaneIndex, note.ParentNote.Width);
                            if (noteRect.Contains(scorePos))
                            {
                                int beforeOffset = action.Offset;
                                return actionNoteHandler(action)
                                    .Finally(() =>
                                    {
                                        OperationManager.Push(new ChangeAirActionOffsetOperation(action, beforeOffset, action.Offset));
                                    });
                            }
                        }
                    }

                    Func<TappableBase, IObservable<MouseEventArgs>> moveTappableNoteHandler = note =>
                    {
                        int beforeLaneIndex = note.LaneIndex;
                        return mouseMove
                            .TakeUntil(mouseUp)
                            .Do(q =>
                            {
                                var currentScorePos = matrix.TransformPoint(q.Location);
                                note.Tick = Math.Max(GetQuantizedTick(GetTickFromYPosition(currentScorePos.Y)), 0);
                                int xdiff = (int)((currentScorePos.X - scorePos.X) / (UnitLaneWidth + BorderThickness));
                                int laneIndex = beforeLaneIndex + xdiff;
                                note.LaneIndex = Math.Min(Constants.LanesCount - note.Width, Math.Max(0, laneIndex));
                                Cursor.Current = Cursors.SizeAll;
                            })
                            .Finally(() => Cursor.Current = Cursors.Default);
                    };

                    Func<TappableBase, IObservable<MouseEventArgs>> tappableNoteLeftThumbHandler = note =>
                    {
                        var beforePos = new ChangeShortNoteWidthOperation.NotePosition(note.LaneIndex, note.Width);
                        return mouseMove
                            .TakeUntil(mouseUp)
                            .Do(q =>
                            {
                                var currentScorePos = matrix.TransformPoint(q.Location);
                                int xdiff = (int)((currentScorePos.X - scorePos.X) / (UnitLaneWidth + BorderThickness));
                                xdiff = Math.Min(beforePos.Width - 1, Math.Max(-beforePos.LaneIndex, xdiff));
                                int width = beforePos.Width - xdiff;
                                int laneIndex = beforePos.LaneIndex + xdiff;
                                //System.Diagnostics.Debug.WriteLine("xdiff: {0}, width: {1}, laneIndex: {2}", xdiff, width, laneIndex);
                                note.Width = Math.Min(Constants.LanesCount - note.LaneIndex, Math.Max(1, width));
                                note.LaneIndex = Math.Min(Constants.LanesCount - note.Width, Math.Max(0, laneIndex));
                                Cursor.Current = Cursors.SizeWE;
                            })
                            .Finally(() =>
                            {
                                Cursor.Current = Cursors.Default;
                                LastWidth = note.Width;
                            });
                    };

                    Func<TappableBase, IObservable<MouseEventArgs>> tappableNoteRightThumbHandler = note =>
                    {
                        int beforeWidth = note.Width;
                        return mouseMove
                            .TakeUntil(mouseUp)
                            .Do(q =>
                            {
                                var currentScorePos = matrix.TransformPoint(q.Location);
                                int xdiff = (int)((currentScorePos.X - scorePos.X) / (UnitLaneWidth + BorderThickness));
                                int width = beforeWidth + xdiff;
                                note.Width = Math.Min(Constants.LanesCount - note.LaneIndex, Math.Max(1, width));
                                Cursor.Current = Cursors.SizeWE;
                            })
                            .Finally(() =>
                            {
                                Cursor.Current = Cursors.Default;
                                LastWidth = note.Width;
                            });
                    };


                    Func<TappableBase, IObservable<MouseEventArgs>> shortNoteHandler = note =>
                    {
                        RectangleF rect = GetClickableRectFromNotePosition(note.Tick, note.LaneIndex, note.Width);
                        RectangleF leftThumb = new RectangleF(rect.X, rect.Y, rect.Width * 0.2f, rect.Height);
                        RectangleF rightThumb = new RectangleF(rect.Right - rect.Width * 0.2f, rect.Y, rect.Width * 0.2f, rect.Height);
                        // ノートの左側
                        if (leftThumb.Contains(scorePos))
                        {
                            var beforePos = new ChangeShortNoteWidthOperation.NotePosition(note.LaneIndex, note.Width);
                            return tappableNoteLeftThumbHandler(note)
                                .Finally(() =>
                                {
                                    var afterPos = new ChangeShortNoteWidthOperation.NotePosition(note.LaneIndex, note.Width);
                                    OperationManager.Push(new ChangeShortNoteWidthOperation(note, beforePos, afterPos));
                                });
                        }

                        // ノートの右側
                        if (rightThumb.Contains(scorePos))
                        {
                            var beforePos = new ChangeShortNoteWidthOperation.NotePosition(note.LaneIndex, note.Width);
                            return tappableNoteRightThumbHandler(note)
                                .Finally(() =>
                                {
                                    var afterPos = new ChangeShortNoteWidthOperation.NotePosition(note.LaneIndex, note.Width);
                                    OperationManager.Push(new ChangeShortNoteWidthOperation(note, beforePos, afterPos));
                                });
                        }

                        // ノート本体
                        if (rect.Contains(scorePos))
                        {
                            var beforePos = new MoveShortNoteOperation.NotePosition(note.Tick, note.LaneIndex);
                            return moveTappableNoteHandler(note)
                                .Finally(() =>
                                {
                                    var afterPos = new MoveShortNoteOperation.NotePosition(note.Tick, note.LaneIndex);
                                    OperationManager.Push(new MoveShortNoteOperation(note, beforePos, afterPos));
                                });
                        }

                        return null;
                    };

                    Func<Hold, IObservable<MouseEventArgs>> holdDurationHandler = hold =>
                    {
                        return mouseMove.TakeUntil(mouseUp)
                            .Do(q =>
                            {
                                var currentCursorPos = matrix.TransformPoint(q.Location);
                                hold.Duration = Math.Max(QuantizeTick, GetQuantizedTick(GetTickFromYPosition(currentCursorPos.Y)) - hold.StartTick);
                                Cursor.Current = Cursors.SizeNS;
                            })
                            .Finally(() => Cursor.Current = Cursors.Default);
                    };

                    Func<Slide.StepTap, IObservable<MouseEventArgs>> slideStepNoteHandler = step =>
                    {
                        int beforeLaneIndexOffset = step.LaneIndexOffset;
                        var offsets = new HashSet<int>(step.ParentNote.StepNotes.Select(q => q.TickOffset));
                        int maxOffset = offsets.Max();
                        bool isMaxOffsetStep = step.TickOffset == maxOffset;
                        offsets.Remove(step.TickOffset);
                        return mouseMove
                            .TakeUntil(mouseUp)
                            .Do(q =>
                            {
                                var currentScorePos = matrix.TransformPoint(q.Location);
                                int offset = GetQuantizedTick(GetTickFromYPosition(currentScorePos.Y)) - step.ParentNote.StartTick;
                                int xdiff = (int)((currentScorePos.X - scorePos.X) / (UnitLaneWidth + BorderThickness));
                                int laneIndexOffset = beforeLaneIndexOffset + xdiff;
                                step.LaneIndexOffset = Math.Min(Constants.LanesCount - step.ParentNote.Width - step.ParentNote.StartLaneIndex, Math.Max(-step.ParentNote.StartLaneIndex, laneIndexOffset));
                                // 最終Step以降に移動はさせないし同じTickに置かせもしない
                                if ((!isMaxOffsetStep && offset > maxOffset) || offsets.Contains(offset) || offset <= 0) return;
                                step.TickOffset = offset;
                                Cursor.Current = Cursors.SizeAll;
                            })
                            .Finally(() => Cursor.Current = Cursors.Default);
                    };

                    Func<Slide, IObservable<MouseEventArgs>> slideHandler = slide =>
                    {
                        foreach (var step in slide.StepNotes)
                        {
                            RectangleF stepRect = GetClickableRectFromNotePosition(step.Tick, step.LaneIndex, step.Width);
                            // AIR or AIR-ACTION追加時で最終Stepだったら動かさない
                            if (stepRect.Contains(scorePos) && (slide.StepNotes.Max(q => q.TickOffset) != step.TickOffset || !(NoteType.Air | NoteType.AirAction).HasFlag(NewNoteType)))
                            {
                                var beforeStepPos = new MoveSlideStepNoteOperation.NotePosition(step.TickOffset, step.LaneIndexOffset);
                                return slideStepNoteHandler(step)
                                    .Finally(() =>
                                    {
                                        var afterPos = new MoveSlideStepNoteOperation.NotePosition(step.TickOffset, step.LaneIndexOffset);
                                        OperationManager.Push(new MoveSlideStepNoteOperation(step, beforeStepPos, afterPos));
                                    });
                            }
                        }

                        RectangleF startRect = GetClickableRectFromNotePosition(slide.StartNote.Tick, slide.StartNote.LaneIndex, slide.StartNote.Width);
                        RectangleF leftThumbRect = new RectangleF(startRect.Left, startRect.Top, startRect.Width * 0.2f, startRect.Height);
                        RectangleF rightThumbRect = new RectangleF(startRect.Right - startRect.Width * 0.2f, startRect.Top, startRect.Width * 0.2f, startRect.Height);

                        int leftStepLaneIndexOffset = Math.Min(0, slide.StepNotes.Min(q => q.LaneIndexOffset));
                        int rightStepLaneIndexOffset = Math.Max(0, slide.StepNotes.Max(q => q.LaneIndexOffset));

                        var beforePos = new MoveSlideOperation.NotePosition(slide.StartTick, slide.StartLaneIndex, slide.Width);
                        if (leftThumbRect.Contains(scorePos))
                        {
                            return mouseMove
                                .TakeUntil(mouseUp)
                                .Do(q =>
                                {
                                    var currentScorePos = matrix.TransformPoint(q.Location);
                                    int xdiff = (int)((currentScorePos.X - scorePos.X) / (UnitLaneWidth + BorderThickness));
                                    xdiff = Math.Min(beforePos.Width - 1, Math.Max(-beforePos.StartLaneIndex - leftStepLaneIndexOffset, xdiff));
                                    int width = beforePos.Width - xdiff;
                                    int laneIndex = beforePos.StartLaneIndex + xdiff;
                                    slide.StartLaneIndex = Math.Min(Constants.LanesCount - slide.Width - rightStepLaneIndexOffset, Math.Max(-leftStepLaneIndexOffset, laneIndex));
                                    slide.Width = Math.Min(Constants.LanesCount - slide.StartLaneIndex - rightStepLaneIndexOffset, Math.Max(1, width));
                                    Cursor.Current = Cursors.SizeWE;
                                })
                                .Finally(() =>
                                {
                                    var afterPos = new MoveSlideOperation.NotePosition(slide.StartTick, slide.StartLaneIndex, slide.Width);
                                    OperationManager.Push(new MoveSlideOperation(slide, beforePos, afterPos));
                                    Cursor.Current = Cursors.Default;
                                    LastWidth = slide.Width;
                                });
                        }

                        if (rightThumbRect.Contains(scorePos))
                        {
                            return mouseMove
                                .TakeUntil(mouseUp)
                                .Do(q =>
                                {
                                    var currentScorePos = matrix.TransformPoint(q.Location);
                                    int xdiff = (int)((currentScorePos.X - scorePos.X) / (UnitLaneWidth + BorderThickness));
                                    int width = beforePos.Width + xdiff;
                                    slide.Width = Math.Min(Constants.LanesCount - slide.StartLaneIndex - rightStepLaneIndexOffset, Math.Max(1, width));
                                    Cursor.Current = Cursors.SizeWE;
                                })
                                .Finally(() =>
                                {
                                    var afterPos = new MoveSlideOperation.NotePosition(slide.StartTick, slide.StartLaneIndex, slide.Width);
                                    OperationManager.Push(new MoveSlideOperation(slide, beforePos, afterPos));
                                    Cursor.Current = Cursors.Default;
                                    LastWidth = slide.Width;
                                });
                        }

                        if (startRect.Contains(scorePos))
                        {
                            int beforeLaneIndex = slide.StartNote.LaneIndex;
                            return mouseMove
                                .TakeUntil(mouseUp)
                                .Do(q =>
                                {
                                    var currentScorePos = matrix.TransformPoint(q.Location);
                                    slide.StartTick = Math.Max(GetQuantizedTick(GetTickFromYPosition(currentScorePos.Y)), 0);
                                    int xdiff = (int)((currentScorePos.X - scorePos.X) / (UnitLaneWidth + BorderThickness));
                                    int laneIndex = beforeLaneIndex + xdiff;
                                    slide.StartLaneIndex = Math.Min(Constants.LanesCount - slide.Width - rightStepLaneIndexOffset, Math.Max(-leftStepLaneIndexOffset, laneIndex));
                                    Cursor.Current = Cursors.SizeAll;
                                })
                                .Finally(() =>
                                {
                                    var afterPos = new MoveSlideOperation.NotePosition(slide.StartTick, slide.StartLaneIndex, slide.Width);
                                    OperationManager.Push(new MoveSlideOperation(slide, beforePos, afterPos));
                                    Cursor.Current = Cursors.Default;
                                    LastWidth = slide.Width;
                                });
                        }

                        return null;
                    };

                    Func<Hold, IObservable<MouseEventArgs>> holdHandler = hold =>
                    {
                        // HOLD長さ変更
                        if (GetClickableRectFromNotePosition(hold.EndNote.Tick, hold.LaneIndex, hold.Width).Contains(scorePos) && !(NoteType.Air | NoteType.AirAction).HasFlag(NewNoteType))
                        {
                            int beforeDuration = hold.Duration;
                            return holdDurationHandler(hold)
                                .Finally(() => OperationManager.Push(new ChangeHoldDurationOperation(hold, beforeDuration, hold.Duration)));
                        }

                        RectangleF startRect = GetClickableRectFromNotePosition(hold.StartTick, hold.LaneIndex, hold.Width);
                        RectangleF leftThumbRect = new RectangleF(startRect.Left, startRect.Top, startRect.Width * 0.2f, startRect.Height);
                        RectangleF rightThumbRect = new RectangleF(startRect.Right - startRect.Width * 0.2f, startRect.Top, startRect.Width * 0.2f, startRect.Height);

                        var beforePos = new ChangeHoldPositionOperation.NotePosition(hold.StartTick, hold.LaneIndex, hold.Width);
                        if (leftThumbRect.Contains(scorePos))
                        {
                            return mouseMove
                                .TakeUntil(mouseUp)
                                .Do(q =>
                                {
                                    var currentScorePos = matrix.TransformPoint(q.Location);
                                    int xdiff = (int)((currentScorePos.X - scorePos.X) / (UnitLaneWidth + BorderThickness));
                                    xdiff = Math.Min(beforePos.Width - 1, Math.Max(-beforePos.LaneIndex, xdiff));
                                    int width = beforePos.Width - xdiff;
                                    int laneIndex = beforePos.LaneIndex + xdiff;
                                    hold.Width = Math.Min(Constants.LanesCount - hold.LaneIndex, Math.Max(1, width));
                                    hold.LaneIndex = Math.Min(Constants.LanesCount - hold.Width, Math.Max(0, laneIndex));
                                    Cursor.Current = Cursors.SizeWE;
                                })
                                .Finally(() =>
                                {
                                    var afterPos = new ChangeHoldPositionOperation.NotePosition(hold.StartTick, hold.LaneIndex, hold.Width);
                                    OperationManager.Push(new ChangeHoldPositionOperation(hold, beforePos, afterPos));
                                    Cursor.Current = Cursors.Default;
                                    LastWidth = hold.Width;
                                });
                        }

                        if (rightThumbRect.Contains(scorePos))
                        {
                            return mouseMove
                                .TakeUntil(mouseUp)
                                .Do(q =>
                                {
                                    var currentScorePos = matrix.TransformPoint(q.Location);
                                    int xdiff = (int)((currentScorePos.X - scorePos.X) / (UnitLaneWidth + BorderThickness));
                                    int width = beforePos.Width + xdiff;
                                    hold.Width = Math.Min(Constants.LanesCount - hold.LaneIndex, Math.Max(1, width));
                                    Cursor.Current = Cursors.SizeWE;
                                })
                                .Finally(() =>
                                {
                                    var afterPos = new ChangeHoldPositionOperation.NotePosition(hold.StartTick, hold.LaneIndex, hold.Width);
                                    OperationManager.Push(new ChangeHoldPositionOperation(hold, beforePos, afterPos));
                                    Cursor.Current = Cursors.Default;
                                    LastWidth = hold.Width;
                                });
                        }

                        if (startRect.Contains(scorePos))
                        {
                            return mouseMove
                                .TakeUntil(mouseUp)
                                .Do(q =>
                                {
                                    var currentScorePos = matrix.TransformPoint(q.Location);
                                    hold.StartTick = Math.Max(GetQuantizedTick(GetTickFromYPosition(currentScorePos.Y)), 0);
                                    int xdiff = (int)((currentScorePos.X - scorePos.X) / (UnitLaneWidth + BorderThickness));
                                    int laneIndex = beforePos.LaneIndex + xdiff;
                                    hold.LaneIndex = Math.Min(Constants.LanesCount - hold.Width, Math.Max(0, laneIndex));
                                    Cursor.Current = Cursors.SizeAll;
                                })
                                .Finally(() =>
                                {
                                    var afterPos = new ChangeHoldPositionOperation.NotePosition(hold.StartTick, hold.LaneIndex, hold.Width);
                                    OperationManager.Push(new ChangeHoldPositionOperation(hold, beforePos, afterPos));
                                    Cursor.Current = Cursors.Default;
                                    LastWidth = hold.Width;
                                });
                        }

                        return null;
                    };

                    if (!(NoteType.Air | NoteType.AirAction).HasFlag(NewNoteType))
                    {
                        foreach (var note in Notes.ExTaps.Where(q => q.Tick >= HeadTick && q.Tick <= tailTick))
                        {
                            var subscription = shortNoteHandler(note);
                            if (subscription != null) return subscription;
                        }

                        foreach (var note in Notes.Taps.Where(q => q.Tick >= HeadTick && q.Tick <= tailTick))
                        {
                            var subscription = shortNoteHandler(note);
                            if (subscription != null) return subscription;
                        }

                        foreach (var note in Notes.Flicks.Where(q => q.Tick >= HeadTick && q.Tick <= tailTick))
                        {
                            var subscription = shortNoteHandler(note);
                            if (subscription != null) return subscription;
                        }

                        foreach (var note in Notes.Damages.Where(q => q.Tick >= HeadTick && q.Tick <= tailTick))
                        {
                            var subscription = shortNoteHandler(note);
                            if (subscription != null) return subscription;
                        }

                        foreach (var note in Notes.Slides.Where(q => q.StartTick <= tailTick && q.StartTick + q.GetDuration() >= HeadTick))
                        {
                            var subscription = slideHandler(note);
                            if (subscription != null) return subscription;
                        }

                        foreach (var note in Notes.Holds.Where(q => q.StartTick <= tailTick && q.StartTick + q.GetDuration() >= HeadTick))
                        {
                            var subscription = holdHandler(note);
                            if (subscription != null) return subscription;
                        }
                    }

                    // なんもねえなら追加だァ！
                    if ((NoteType.Tap | NoteType.ExTap | NoteType.Flick | NoteType.Damage).HasFlag(NewNoteType))
                    {
                        TappableBase newNote = null;
                        IOperation op = null;
                        switch (NewNoteType)
                        {
                            case NoteType.Tap:
                                var tap = new Tap();
                                Notes.Add(tap);
                                newNote = tap;
                                op = new InsertTapOperation(Notes, tap);
                                break;

                            case NoteType.ExTap:
                                var extap = new ExTap();
                                Notes.Add(extap);
                                newNote = extap;
                                op = new InsertExTapOperation(Notes, extap);
                                break;

                            case NoteType.Flick:
                                var flick = new Flick();
                                Notes.Add(flick);
                                newNote = flick;
                                op = new InsertFlickOperation(Notes, flick);
                                break;

                            case NoteType.Damage:
                                var damage = new Damage();
                                Notes.Add(damage);
                                newNote = damage;
                                op = new InsertDamageOperation(Notes, damage);
                                break;
                        }
                        newNote.Width = LastWidth;
                        newNote.Tick = GetQuantizedTick(GetTickFromYPosition(scorePos.Y));
                        int newNoteLaneIndex = (int)(scorePos.X / (UnitLaneWidth + BorderThickness)) - newNote.Width / 2;
                        newNoteLaneIndex = Math.Min(Constants.LanesCount - newNote.Width, Math.Max(0, newNoteLaneIndex));
                        newNote.LaneIndex = newNoteLaneIndex;
                        Invalidate();
                        return moveTappableNoteHandler(newNote)
                            .Finally(() => OperationManager.Push(op));
                    }
                    else
                    {
                        int newNoteLaneIndex;
                        var airables = Enumerable.Empty<IAirable>();
                        airables = airables.Concat(Notes.Taps);
                        airables = airables.Concat(Notes.ExTaps);
                        airables = airables.Concat(Notes.Flicks);
                        airables = airables.Concat(Notes.Damages);
                        airables = airables.Concat(Notes.Holds.Select(q => q.EndNote));
                        airables = airables.Concat(Notes.Slides.Select(q => q.StepNotes.OrderByDescending(r => r.TickOffset).First()));

                        switch (NewNoteType)
                        {
                            case NoteType.Hold:
                                var hold = new Hold
                                {
                                    StartTick = GetQuantizedTick(GetTickFromYPosition(scorePos.Y)),
                                    Width = LastWidth,
                                    Duration = QuantizeTick
                                };
                                newNoteLaneIndex = (int)(scorePos.X / (UnitLaneWidth + BorderThickness)) - hold.Width / 2;
                                hold.LaneIndex = Math.Min(Constants.LanesCount - hold.Width, Math.Max(0, newNoteLaneIndex));
                                Notes.Add(hold);
                                Invalidate();
                                return holdDurationHandler(hold)
                                    .Finally(() => OperationManager.Push(new InsertHoldOperation(Notes, hold)));

                            case NoteType.Slide:
                                // 中継点
                                foreach (var note in Notes.Slides)
                                {
                                    var bg = new Slide.TapBase[] { note.StartNote }.Concat(note.StepNotes.OrderBy(q => q.Tick)).ToList();
                                    for (int i = 0; i < bg.Count - 1; i++)
                                    {
                                        // 描画時のコードコピペつらい
                                        var path = note.GetBackgroundPath(
                                            (UnitLaneWidth + BorderThickness) * note.Width - BorderThickness,
                                            (UnitLaneWidth + BorderThickness) * bg[i].LaneIndex,
                                            GetYPositionFromTick(bg[i].Tick),
                                            (UnitLaneWidth + BorderThickness) * bg[i + 1].LaneIndex,
                                            GetYPositionFromTick(bg[i + 1].Tick));
                                        if (path.PathPoints.ContainsPoint(scorePos))
                                        {
                                            int tickOffset = GetQuantizedTick(GetTickFromYPosition(scorePos.Y)) - note.StartTick;
                                            // 同一Tickに追加させない
                                            if (tickOffset != 0 && !note.StepNotes.Any(q => q.TickOffset == tickOffset))
                                            {
                                                int laneIndex = (int)(scorePos.X / (UnitLaneWidth + BorderThickness)) - note.Width / 2;
                                                laneIndex = Math.Min(Constants.LanesCount - note.Width, Math.Max(0, laneIndex));
                                                int laneIndexOffset = laneIndex - note.StartLaneIndex;
                                                var newStep = new Slide.StepTap(note)
                                                {
                                                    TickOffset = tickOffset,
                                                    LaneIndexOffset = laneIndexOffset,
                                                    IsVisible = IsNewSlideStepVisible
                                                };
                                                note.StepNotes.Add(newStep);
                                                Invalidate();
                                                return slideStepNoteHandler(newStep)
                                                    .Finally(() => OperationManager.Push(new InsertSlideStepNoteOperation(note, newStep)));
                                            }
                                        }
                                    }
                                }

                                // 新規SLIDE
                                var slide = new Slide()
                                {
                                    StartTick = GetQuantizedTick(GetTickFromYPosition(scorePos.Y)),
                                    Width = LastWidth
                                };
                                newNoteLaneIndex = (int)(scorePos.X / (UnitLaneWidth + BorderThickness)) - slide.Width / 2;
                                slide.StartLaneIndex = Math.Min(Constants.LanesCount - slide.Width, Math.Max(0, newNoteLaneIndex));
                                var step = new Slide.StepTap(slide) { TickOffset = QuantizeTick };
                                slide.StepNotes.Add(step);
                                Notes.Add(slide);
                                Invalidate();
                                return slideStepNoteHandler(step)
                                    .Finally(() => OperationManager.Push(new InsertSlideOperation(Notes, slide)));

                            case NoteType.Air:
                                foreach (var note in airables)
                                {
                                    RectangleF rect = GetClickableRectFromNotePosition(note.Tick, note.LaneIndex, note.Width);
                                    if (rect.Contains(scorePos))
                                    {
                                        // 既に配置されていれば追加しない
                                        if (Notes.GetReferencedAir(note).Count() > 0) break;
                                        return mouseMove
                                            .TakeUntil(mouseUp)
                                            .Count()
                                            .Zip(mouseUp, (q, r) => new { Args = r, Count = q })
                                            .Where(q => q.Count == 0)
                                            .Select(q => q.Args)
                                            .Do(q =>
                                            {
                                                var air = new Air(note)
                                                {
                                                    VerticalDirection = AirDirection.VerticalDirection,
                                                    HorizontalDirection = AirDirection.HorizontalDirection
                                                };
                                                Notes.Add(air);
                                                Invalidate();
                                                OperationManager.Push(new InsertAirOperation(Notes, air));
                                            });
                                    }
                                }
                                break;

                            case NoteType.AirAction:
                                foreach (var note in Notes.AirActions)
                                {
                                    var size = new SizeF(UnitLaneWidth / 2, GetYPositionFromTick(note.ActionNotes.Max(q => q.Offset)));
                                    var rect = new RectangleF(
                                        (UnitLaneWidth + BorderThickness) * (note.ParentNote.LaneIndex + note.ParentNote.Width / 2f) - size.Width / 2,
                                        GetYPositionFromTick(note.ParentNote.Tick),
                                        size.Width, size.Height);
                                    if (rect.Contains(scorePos))
                                    {
                                        int offset = GetQuantizedTick(GetTickFromYPosition(scorePos.Y)) - note.ParentNote.Tick;
                                        if (!note.ActionNotes.Any(q => q.Offset == offset))
                                        {
                                            var action = new AirAction.ActionNote(note) { Offset = offset };
                                            note.ActionNotes.Add(action);
                                            Invalidate();
                                            return actionNoteHandler(action)
                                                .Finally(() => OperationManager.Push(new InsertAirActionNoteOperation(note, action)));
                                        }
                                    }
                                }

                                foreach (var note in airables)
                                {
                                    RectangleF rect = GetClickableRectFromNotePosition(note.Tick, note.LaneIndex, note.Width);
                                    if (rect.Contains(scorePos))
                                    {
                                        // 既に配置されていれば追加しない
                                        if (Notes.GetReferencedAirAction(note).Count() > 0) break;
                                        var airAction = new AirAction(note);
                                        var action = new AirAction.ActionNote(airAction) { Offset = QuantizeTick };
                                        airAction.ActionNotes.Add(action);
                                        Notes.Add(airAction);
                                        Invalidate();
                                        return actionNoteHandler(action)
                                            .Finally(() => OperationManager.Push(new InsertAirActionOperation(Notes, airAction)));
                                    }
                                }
                                break;
                        }
                    }
                    return Observable.Empty<MouseEventArgs>();
                }).Subscribe(p => Invalidate());

            var eraseSubscription = mouseDown
                .Where(p => Editable)
                .Where(p => p.Button == MouseButtons.Left && EditMode == EditMode.Erase)
                .SelectMany(p => mouseMove
                    .TakeUntil(mouseUp)
                    .Count()
                    .Zip(mouseUp, (q, r) => new { Pos = r.Location, Count = q })
                )
                .Where(p => p.Count == 0) // ドラッグなし
                .Do(p =>
                {
                    Matrix matrix = GetDrawingMatrix(new Matrix());
                    matrix.Invert();
                    PointF scorePos = matrix.TransformPoint(p.Pos);

                    Func<IAirable, IEnumerable<IOperation>> removeReferencedAirs = airable =>
                    {
                        var airs = Notes.GetReferencedAir(airable).ToList().Select(q =>
                        {
                            Notes.Remove(q);
                            return new RemoveAirOperation(Notes, q);
                        }).ToList();
                        var airActions = Notes.GetReferencedAirAction(airable).ToList().Select(q =>
                        {
                            Notes.Remove(q);
                            return new RemoveAirActionOperation(Notes, q);
                        }).ToList();

                        return airs.Cast<IOperation>().Concat(airActions);
                    };

                    foreach (var note in Notes.Airs)
                    {
                        RectangleF rect = note.GetDestRectangle(GetRectFromNotePosition(note.ParentNote.Tick, note.ParentNote.LaneIndex, note.ParentNote.Width));
                        if (rect.Contains(scorePos))
                        {
                            Notes.Remove(note);
                            OperationManager.Push(new RemoveAirOperation(Notes, note));
                            return;
                        }
                    }

                    foreach (var note in Notes.AirActions)
                    {
                        foreach (var action in note.ActionNotes)
                        {
                            RectangleF rect = GetClickableRectFromNotePosition(note.StartTick + action.Offset, note.ParentNote.LaneIndex, note.ParentNote.Width);
                            if (rect.Contains(scorePos))
                            {
                                if (note.ActionNotes.Count == 1)
                                {
                                    Notes.Remove(note);
                                    OperationManager.Push(new RemoveAirActionOperation(Notes, note));
                                }
                                else
                                {
                                    note.ActionNotes.Remove(action);
                                    OperationManager.Push(new RemoveAirActionNoteOperation(note, action));
                                }
                                return;
                            }
                        }
                    }

                    foreach (var note in Notes.Flicks)
                    {
                        RectangleF rect = GetClickableRectFromNotePosition(note.Tick, note.LaneIndex, note.Width);
                        if (rect.Contains(scorePos))
                        {
                            var airOp = removeReferencedAirs(note).ToList();
                            var op = new RemoveFlickOperation(Notes, note);
                            Notes.Remove(note);
                            if (airOp.Count > 0)
                            {
                                OperationManager.Push(new CompositeOperation(op.Description, new IOperation[] { op }.Concat(airOp)));
                            }
                            else
                            {
                                OperationManager.Push(op);
                            }
                            return;
                        }
                    }

                    foreach (var note in Notes.Damages)
                    {
                        RectangleF rect = GetClickableRectFromNotePosition(note.Tick, note.LaneIndex, note.Width);
                        if (rect.Contains(scorePos))
                        {
                            var airOp = removeReferencedAirs(note).ToList();
                            var op = new RemoveDamageOperation(Notes, note);
                            Notes.Remove(note);
                            if (airOp.Count > 0)
                            {
                                OperationManager.Push(new CompositeOperation(op.Description, new IOperation[] { op }.Concat(airOp)));
                            }
                            else
                            {
                                OperationManager.Push(op);
                            }
                            return;
                        }
                    }

                    foreach (var note in Notes.ExTaps)
                    {
                        RectangleF rect = GetClickableRectFromNotePosition(note.Tick, note.LaneIndex, note.Width);
                        if (rect.Contains(scorePos))
                        {
                            var airOp = removeReferencedAirs(note).ToList();
                            var op = new RemoveExTapOperation(Notes, note);
                            Notes.Remove(note);
                            if (airOp.Count > 0)
                            {
                                OperationManager.Push(new CompositeOperation(op.Description, new IOperation[] { op }.Concat(airOp)));
                            }
                            else
                            {
                                OperationManager.Push(op);
                            }
                            return;
                        }
                    }

                    foreach (var note in Notes.Taps)
                    {
                        RectangleF rect = GetClickableRectFromNotePosition(note.Tick, note.LaneIndex, note.Width);
                        if (rect.Contains(scorePos))
                        {
                            var airOp = removeReferencedAirs(note).ToList();
                            var op = new RemoveTapOperation(Notes, note);
                            Notes.Remove(note);
                            if (airOp.Count > 0)
                            {
                                OperationManager.Push(new CompositeOperation(op.Description, new IOperation[] { op }.Concat(airOp)));
                            }
                            else
                            {
                                OperationManager.Push(op);
                            }
                            return;
                        }
                    }

                    foreach (var slide in Notes.Slides)
                    {
                        foreach (var step in slide.StepNotes.OrderBy(q => q.TickOffset).Take(slide.StepNotes.Count - 1))
                        {
                            RectangleF rect = GetClickableRectFromNotePosition(step.Tick, step.LaneIndex, step.Width);
                            if (rect.Contains(scorePos))
                            {
                                slide.StepNotes.Remove(step);
                                OperationManager.Push(new RemoveSlideStepNoteOperation(slide, step));
                                return;
                            }
                        }

                        RectangleF startRect = GetClickableRectFromNotePosition(slide.StartTick, slide.StartLaneIndex, slide.Width);
                        if (startRect.Contains(scorePos))
                        {
                            var airOp = slide.StepNotes.SelectMany(q => removeReferencedAirs(q)).ToList();
                            var op = new RemoveSlideOperation(Notes, slide);
                            Notes.Remove(slide);
                            if (airOp.Count > 0)
                            {
                                OperationManager.Push(new CompositeOperation(op.Description, new IOperation[] { op }.Concat(airOp)));
                            }
                            else
                            {
                                OperationManager.Push(op);
                            }
                            return;
                        }
                    }

                    foreach (var hold in Notes.Holds)
                    {
                        RectangleF rect = GetClickableRectFromNotePosition(hold.StartTick, hold.LaneIndex, hold.Width);
                        if (rect.Contains(scorePos))
                        {
                            var airOp = removeReferencedAirs(hold.EndNote).ToList();
                            var op = new RemoveHoldOperation(Notes, hold);
                            Notes.Remove(hold);
                            if (airOp.Count > 0)
                            {
                                OperationManager.Push(new CompositeOperation(op.Description, new IOperation[] { op }.Concat(airOp)));
                            }
                            else
                            {
                                OperationManager.Push(op);
                            }
                            return;
                        }
                    }
                })
                .Subscribe(p => Invalidate());

            Subscriptions.Add(editSubscription);
            Subscriptions.Add(eraseSubscription);
        }

        protected override void OnPaint(PaintEventArgs pe)
        {
            base.OnPaint(pe);

            // Y軸の正方向をTick増加方向として描画 (y = 0 はコントロール下端)
            // コントロールの中心に描画したいなら後でTranslateしといてね
            var prevMatrix = pe.Graphics.Transform;
            pe.Graphics.Transform = GetDrawingMatrix(prevMatrix);

            float laneWidth = LaneWidth;
            int tailTick = HeadTick + (int)(ClientSize.Height * UnitBeatTick / UnitBeatHeight);

            // レーン分割線描画
            using (var lightPen = new Pen(LaneBorderLightColor, BorderThickness))
            using (var darkPen = new Pen(LaneBorderDarkColor, BorderThickness))
            {
                for (int i = 0; i <= Constants.LanesCount; i++)
                {
                    float x = i * (UnitLaneWidth + BorderThickness);
                    pe.Graphics.DrawLine(i % 2 == 0 ? lightPen : darkPen, x, GetYPositionFromTick(HeadTick), x, GetYPositionFromTick(tailTick));
                }
            }


            // 時間ガイドの描画
            using (var beatPen = new Pen(BeatLineColor, BorderThickness))
            using (var barPen = new Pen(BarLineColor, BorderThickness))
            {
                for (int i = HeadTick / UnitBeatTick; i * UnitBeatTick < tailTick; i++)
                {
                    float y = i * UnitBeatHeight;
                    // 4分の4拍子で1小節ごとに小節区切り
                    pe.Graphics.DrawLine(i % 4 == 0 ? barPen : beatPen, 0, y, laneWidth, y);
                }
            }

            // ノート描画
            var holds = Notes.Holds.Where(p => p.StartTick <= tailTick && p.StartTick + p.GetDuration() >= HeadTick).ToList();
            // ロングノーツ背景
            // HOLD
            foreach (var hold in holds)
            {
                hold.DrawBackground(pe.Graphics, new RectangleF(
                    (UnitLaneWidth + BorderThickness) * hold.LaneIndex + BorderThickness,
                    GetYPositionFromTick(hold.StartTick),
                    (UnitLaneWidth + BorderThickness) * hold.Width - BorderThickness,
                    GetYPositionFromTick(hold.Duration)
                    ));
            }

            // SLIDE
            var slides = Notes.Slides.Where(p => p.StartTick <= tailTick && p.StartTick + p.GetDuration() >= HeadTick).ToList();
            foreach (var slide in slides)
            {
                var bg = new Slide.TapBase[] { slide.StartNote }.Concat(slide.StepNotes.OrderBy(p => p.Tick)).ToList();
                var visibleSteps = new Slide.TapBase[] { slide.StartNote }.Concat(slide.StepNotes.Where(p => p.IsVisible).OrderBy(p => p.Tick)).ToList();
                for (int i = 0; i < bg.Count - 1; i++)
                {
                    slide.DrawBackground(pe.Graphics,
                        (UnitLaneWidth + BorderThickness) * slide.Width - BorderThickness,
                        (UnitLaneWidth + BorderThickness) * bg[i].LaneIndex,
                        GetYPositionFromTick(bg[i].Tick),
                        (UnitLaneWidth + BorderThickness) * bg[i + 1].LaneIndex,
                        GetYPositionFromTick(bg[i + 1].Tick) + 0.4f,
                        GetYPositionFromTick(visibleSteps.Last(p => p.Tick <= bg[i].Tick).Tick),
                        GetYPositionFromTick(visibleSteps.First(p => p.Tick >= bg[i + 1].Tick).Tick),
                        ShortNoteHeight);
                }
            }

            // AIR-ACTION(ガイド線)
            var airActions = Notes.AirActions.Where(p => p.StartTick <= tailTick && p.StartTick + p.GetDuration() >= HeadTick).ToList();
            foreach (var note in airActions)
            {
                note.DrawLine(pe.Graphics,
                    (UnitLaneWidth + BorderThickness) * (note.ParentNote.LaneIndex + note.ParentNote.Width / 2f),
                    GetYPositionFromTick(note.StartTick),
                    GetYPositionFromTick(note.StartTick + note.GetDuration()),
                    ShortNoteHeight);
            }

            // ショートノーツ
            // HOLD始点
            foreach (var hold in holds)
            {
                hold.StartNote.Draw(pe.Graphics, GetRectFromNotePosition(hold.StartTick, hold.LaneIndex, hold.Width));
                hold.EndNote.Draw(pe.Graphics, GetRectFromNotePosition(hold.StartTick + hold.Duration, hold.LaneIndex, hold.Width));
            }

            // SLIDE始点
            foreach (var slide in slides)
            {
                slide.StartNote.Draw(pe.Graphics, GetRectFromNotePosition(slide.StartTick, slide.StartNote.LaneIndex, slide.Width));
                foreach (var step in slide.StepNotes)
                {
                    if (!Editable && !step.IsVisible) continue;
                    step.Draw(pe.Graphics, GetRectFromNotePosition(step.Tick, step.LaneIndex, step.Width));
                }
            }

            // TAP, ExTAP, FLICK, DAMAGE
            foreach (var note in Notes.Taps.Where(p => p.Tick >= HeadTick && p.Tick <= tailTick))
            {
                note.Draw(pe.Graphics, GetRectFromNotePosition(note.Tick, note.LaneIndex, note.Width));
            }

            foreach (var note in Notes.ExTaps.Where(p => p.Tick >= HeadTick && p.Tick <= tailTick))
            {
                note.Draw(pe.Graphics, GetRectFromNotePosition(note.Tick, note.LaneIndex, note.Width));
            }

            foreach (var note in Notes.Flicks.Where(p => p.Tick >= HeadTick && p.Tick <= tailTick))
            {
                note.Draw(pe.Graphics, GetRectFromNotePosition(note.Tick, note.LaneIndex, note.Width));
            }

            foreach (var note in Notes.Damages.Where(p => p.Tick >= HeadTick && p.Tick <= tailTick))
            {
                note.Draw(pe.Graphics, GetRectFromNotePosition(note.Tick, note.LaneIndex, note.Width));
            }

            // AIR-ACTION(ActionNote)
            foreach (var action in airActions)
            {
                foreach (var note in action.ActionNotes)
                {
                    note.Draw(pe.Graphics, GetRectFromNotePosition(action.StartTick + note.Offset, action.ParentNote.LaneIndex, action.ParentNote.Width).Expand(-ShortNoteHeight * 0.28f));
                }
            }

            // AIR
            foreach (var note in Notes.Airs.Where(p => p.Tick >= HeadTick && p.Tick <= tailTick))
            {
                RectangleF rect = GetRectFromNotePosition(note.ParentNote.Tick, note.ParentNote.LaneIndex, note.ParentNote.Width);
                note.Draw(pe.Graphics, rect);
                if (note.ParentNote is LongNoteTapBase)
                {
                    (note.ParentNote as LongNoteTapBase).Draw(pe.Graphics, rect, true);
                }
            }

            // Y軸反転させずにTick = 0をY軸原点とする座標系へ
            pe.Graphics.Transform = GetDrawingMatrix(prevMatrix, false);

            Font font = new Font("MS Gothic", 8);
            SizeF strSize = pe.Graphics.MeasureString("000", font);

            // 小節番号描画(4分の4拍子)
            for (int i = HeadTick / UnitBeatTick / 4; i * UnitBeatTick * 4 <= tailTick; i++)
            {
                var point = new PointF(-strSize.Width, -GetYPositionFromTick(i * UnitBeatTick * 4) - strSize.Height);
                pe.Graphics.DrawString(string.Format("{0:000}", i + 1), font, Brushes.White, point);
            }

            pe.Graphics.Transform = prevMatrix;
        }

        private Matrix GetDrawingMatrix(Matrix baseMatrix)
        {
            return GetDrawingMatrix(baseMatrix, true);
        }

        private Matrix GetDrawingMatrix(Matrix baseMatrix, bool flipY)
        {
            Matrix matrix = baseMatrix.Clone();
            if (flipY)
            {
                // 反転してY軸増加方向を時間軸に
                matrix.Scale(1, -1);
            }
            // ずれたコントロール高さ分を補正
            matrix.Translate(0, ClientSize.Height - 1, MatrixOrder.Append);
            // さらにずらして下端とHeadTickを合わせる
            matrix.Translate(0, HeadTick * UnitBeatHeight / UnitBeatTick, MatrixOrder.Append);
            // 水平方向に対して中央に寄せる
            matrix.Translate((ClientSize.Width - LaneWidth) / 2, 0);

            return matrix;
        }

        private float GetYPositionFromTick(int tick)
        {
            return tick * UnitBeatHeight / UnitBeatTick;
        }

        protected int GetTickFromYPosition(float y)
        {
            return (int)(y * UnitBeatTick / UnitBeatHeight);
        }

        protected int GetQuantizedTick(float tick)
        {
            return (int)Math.Round((float)tick / QuantizeTick) * QuantizeTick;
        }

        private RectangleF GetRectFromNotePosition(int tick, int laneIndex, int width)
        {
            return new RectangleF(
                (UnitLaneWidth + BorderThickness) * laneIndex + BorderThickness,
                GetYPositionFromTick(tick) - ShortNoteHeight / 2,
                (UnitLaneWidth + BorderThickness) * width - BorderThickness,
                ShortNoteHeight
                );
        }

        private RectangleF GetClickableRectFromNotePosition(int tick, int laneIndex, int width)
        {
            return GetRectFromNotePosition(tick, laneIndex, width).Expand(1);
        }

        public void Undo()
        {
            if (!OperationManager.CanUndo) return;
            OperationManager.Undo();
            Invalidate();
        }

        public void Redo()
        {
            if (!OperationManager.CanRedo) return;
            OperationManager.Redo();
            Invalidate();
        }


        public void Load(Components.NoteCollection collection)
        {
            Notes.Load(collection);
            OperationManager.Clear();
            Invalidate();
        }

        public class NoteCollection
        {
            public event EventHandler NoteChanged;

            private List<Tap> taps;
            private List<ExTap> exTaps;
            private List<Hold> holds;
            private List<Slide> slides;
            private List<Air> airs;
            private List<AirAction> airActions;
            private List<Flick> flicks;
            private List<Damage> damages;

            private Dictionary<IAirable, HashSet<Air>> AirDictionary { get; } = new Dictionary<IAirable, HashSet<Air>>();
            private Dictionary<IAirable, HashSet<AirAction>> AirActionDictionary { get; } = new Dictionary<IAirable, HashSet<AirAction>>();

            public IReadOnlyCollection<Tap> Taps { get { return taps; } }
            public IReadOnlyCollection<ExTap> ExTaps { get { return exTaps; } }
            public IReadOnlyCollection<Hold> Holds { get { return holds; } }
            public IReadOnlyCollection<Slide> Slides { get { return slides; } }
            public IReadOnlyCollection<Air> Airs { get { return airs; } }
            public IReadOnlyCollection<AirAction> AirActions { get { return airActions; } }
            public IReadOnlyCollection<Flick> Flicks { get { return flicks; } }
            public IReadOnlyCollection<Damage> Damages { get { return damages; } }

            public NoteCollection()
            {
                taps = new List<Tap>();
                exTaps = new List<ExTap>();
                holds = new List<Hold>();
                slides = new List<Slide>();
                airs = new List<Air>();
                airActions = new List<AirAction>();
                flicks = new List<Flick>();
                damages = new List<Damage>();
            }

            public void Add(Tap note)
            {
                taps.Add(note);
                NoteChanged?.Invoke(this, EventArgs.Empty);
            }

            public void Add(ExTap note)
            {
                exTaps.Add(note);
                NoteChanged?.Invoke(this, EventArgs.Empty);
            }

            public void Add(Hold note)
            {
                holds.Add(note);
                NoteChanged?.Invoke(this, EventArgs.Empty);
            }

            public void Add(Slide note)
            {
                slides.Add(note);
                NoteChanged?.Invoke(this, EventArgs.Empty);
            }

            public void Add(Air note)
            {
                airs.Add(note);
                if (!AirDictionary.ContainsKey(note.ParentNote))
                    AirDictionary.Add(note.ParentNote, new HashSet<Air>());
                AirDictionary[note.ParentNote].Add(note);
                NoteChanged?.Invoke(this, EventArgs.Empty);
            }

            public void Add(AirAction note)
            {
                airActions.Add(note);
                if (!AirActionDictionary.ContainsKey(note.ParentNote))
                    AirActionDictionary.Add(note.ParentNote, new HashSet<AirAction>());
                AirActionDictionary[note.ParentNote].Add(note);
                NoteChanged?.Invoke(this, EventArgs.Empty);
            }

            public void Add(Flick note)
            {
                flicks.Add(note);
                NoteChanged?.Invoke(this, EventArgs.Empty);
            }

            public void Add(Damage note)
            {
                damages.Add(note);
                NoteChanged?.Invoke(this, EventArgs.Empty);
            }


            public void Remove(Tap note)
            {
                taps.Remove(note);
                NoteChanged?.Invoke(this, EventArgs.Empty);
            }

            public void Remove(ExTap note)
            {
                exTaps.Remove(note);
                NoteChanged?.Invoke(this, EventArgs.Empty);
            }

            public void Remove(Hold note)
            {
                holds.Remove(note);
                NoteChanged?.Invoke(this, EventArgs.Empty);
            }

            public void Remove(Slide note)
            {
                slides.Remove(note);
                NoteChanged?.Invoke(this, EventArgs.Empty);
            }

            public void Remove(Air note)
            {
                airs.Remove(note);
                AirDictionary[note.ParentNote].Remove(note);
                NoteChanged?.Invoke(this, EventArgs.Empty);
            }

            public void Remove(AirAction note)
            {
                airActions.Remove(note);
                AirActionDictionary[note.ParentNote].Remove(note);
                NoteChanged?.Invoke(this, EventArgs.Empty);
            }

            public void Remove(Flick note)
            {
                flicks.Remove(note);
                NoteChanged?.Invoke(this, EventArgs.Empty);
            }

            public void Remove(Damage note)
            {
                damages.Remove(note);
                NoteChanged?.Invoke(this, EventArgs.Empty);
            }

            public IEnumerable<Air> GetReferencedAir(IAirable note)
            {
                if (!AirDictionary.ContainsKey(note)) return Enumerable.Empty<Air>();
                return AirDictionary[note];
            }

            public IEnumerable<AirAction> GetReferencedAirAction(IAirable note)
            {
                if (!AirActionDictionary.ContainsKey(note)) return Enumerable.Empty<AirAction>();
                return AirActionDictionary[note];
            }

            public int GetLastTick()
            {
                var shortNotes = Taps.Cast<TappableBase>().Concat(exTaps).Concat(Flicks).Concat(Damages).ToList();
                var longNotes = Holds.Cast<ILongNote>().Concat(Slides).Concat(AirActions).ToList();
                int lastShortNoteTick = shortNotes.Count == 0 ? 0 : shortNotes.Max(p => p.Tick);
                int lastLongNoteTick = longNotes.Count == 0 ? 0 : longNotes.Max(p => p.StartTick + p.GetDuration());
                return Math.Max(lastShortNoteTick, lastLongNoteTick);
            }


            public void Load(Components.NoteCollection collection)
            {
                Clear();

                foreach (var note in collection.Taps) Add(note);
                foreach (var note in collection.ExTaps) Add(note);
                foreach (var note in collection.Holds) Add(note);
                foreach (var note in collection.Slides) Add(note);
                foreach (var note in collection.Airs) Add(note);
                foreach (var note in collection.AirActions) Add(note);
                foreach (var note in collection.Flicks) Add(note);
                foreach (var note in collection.Damages) Add(note);
            }

            public void Clear()
            {
                taps.Clear();
                exTaps.Clear();
                holds.Clear();
                slides.Clear();
                airs.Clear();
                airActions.Clear();
                flicks.Clear();
                damages.Clear();

                AirDictionary.Clear();
                AirActionDictionary.Clear();

                NoteChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public enum EditMode
    {
        Edit,
        Erase
    }

    [Flags]
    public enum NoteType
    {
        Tap = 1,
        ExTap = 1 << 1,
        Hold = 1 << 2,
        Slide = 1 << 3,
        Air = 1 << 4,
        AirAction = 1 << 5,
        Flick = 1 << 6,
        Damage = 1 << 7
    }

    public struct AirDirection
    {
        public VerticalAirDirection VerticalDirection { get; }
        public HorizontalAirDirection HorizontalDirection { get; }

        public AirDirection(VerticalAirDirection verticalDirection, HorizontalAirDirection horizontaiDirection)
        {
            VerticalDirection = verticalDirection;
            HorizontalDirection = horizontaiDirection;
        }
    }
}
