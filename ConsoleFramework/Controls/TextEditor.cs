﻿using System;
using System.Collections.Generic;
using System.Linq;
using ConsoleFramework.Core;
using ConsoleFramework.Events;
using ConsoleFramework.Native;
using ConsoleFramework.Rendering;
using Xaml;

// TODO : move cursorPos + window to another class > ?
namespace ConsoleFramework.Controls {
    /// <summary>
    /// Incapsulates text holder and all the data required to display the content
    /// properly in predictable way. Should be covered by unit tests. Unit tests
    /// can be written easy using steps like theese:
    /// 1. Initialize using some text and initial cursorPos, window values
    /// 2. Apply some commands
    /// 3. Check the result state
    /// </summary>
    public class TextEditorController {
        /// <summary>
        /// Logical cursor position (points to symbol in textItems, not to display coord)
        /// </summary>
        public Point CursorPos { get; set; }

        /// <summary>
        /// Current display window
        /// </summary>
        public Rect Window { get; set; }

        /// <summary>
        /// Current text in editor
        /// </summary>
        private TextHolder textHolder;

        public void WriteToWindow(char[,] buffer) {
            textHolder.WriteToWindow(Window.Left, Window.Top, Window.Width, Window.Height, buffer);
        }

        public string Text {
            get => textHolder.Text;
            set {
                if (textHolder.Text != value) {
                    textHolder.Text = value;
                    CursorPos = new Point();
                    Window = new Rect(new Point(), Window.Size);
                }
            }
        }

        public int LinesCount => textHolder.LinesCount;
        public int ColumnsCount => textHolder.ColumnsCount;

        public TextEditorController(string text, int width, int height) :
            this(new TextHolder(text), new Point(), new Rect(0, 0, width, height)) {
        }

        public TextEditorController(TextHolder textHolder, Point cursorPos, Rect window) {
            this.textHolder = textHolder;
            this.CursorPos = cursorPos;
            this.Window = window;
        }

        public interface ICommand {
            /// <summary>
            /// Returns true if visible content has changed during the operation
            /// (and therefore should be invalidated), false otherwise.
            /// </summary>
            bool Do(TextEditorController controller);

            //void Undo();
        }

        static Point cursorPosToTextPos(Point cursorPos, Rect window) {
            cursorPos.Offset(window.X, window.Y);
            return cursorPos;
        }

        static Point textPosToCursorPos(Point textPos, Rect window) {
            textPos.Offset(-window.X, -window.Y);
            return textPos;
        }

        public class AppendStringCmd : ICommand {
            private readonly string s;

            public AppendStringCmd(string s) {
                this.s = s;
            }

            public bool Do(TextEditorController controller) {
                Point textPos = cursorPosToTextPos(controller.CursorPos, controller.Window);
                Point nextCharPos =
                    controller.textHolder.Insert(textPos.Y, textPos.X, s);

                // Move window to just edited place if need
                var cursor = textPosToCursorPos(nextCharPos, controller.Window);

                if (cursor.X >= controller.Window.Width) {
                    // Move window right if nextChar is outside the window after add char
                    controller.Window =
                        new Rect(controller.Window.X +
                                 cursor.X - controller.Window.Width + 3,
                            controller.Window.Top,
                            controller.Window.Width,
                            controller.Window.Height);
                } else if (cursor.X < 0) {
                    // Move window left if need
                    controller.Window =
                        new Rect(controller.Window.X + cursor.X,
                            controller.Window.Top,
                            controller.Window.Width,
                            controller.Window.Height);
                }

                // Move window down if nextChar is outside the window
                if (cursor.Y >= controller.Window.Height) {
                    controller.Window =
                        new Rect(
                            controller.Window.Left,
                            controller.Window.Top + cursor.Y - controller.Window.Height + 1,
                            controller.Window.Width,
                            controller.Window.Height);
                }

                controller.CursorPos =
                    textPosToCursorPos(nextCharPos, controller.Window);

                return true;
            }
        }
    }

    public class TextHolder {
        // TODO : change to more appropriate data structure
        private List<string> lines;

        public TextHolder(string text) {
            setText(text);
        }

        private void setText(string text) {
            lines = new List<string>(text.Split(new[] {Environment.NewLine}, StringSplitOptions.None));
        }

        public string Text {
            get => string.Join(Environment.NewLine, lines);
            set => setText(value);
        }

        public int LinesCount => lines.Count;
        public int ColumnsCount => lines.Max(it => it.Length);

        /// <summary>
        /// Inserts string after specified position with respect to newline symbols.
        /// Returns the coords (col+ln) of next symbol after inserted.
        /// TODO : write unit test to check return value
        /// </summary>
        public Point Insert(int ln, int col, string s) {
            // There are at least one empty line even if no text at all
            if (ln >= lines.Count) {
                throw new ArgumentException("ln is out of range", nameof(ln));
            }

            string currentLine = lines[ln];
            if (col > currentLine.Length) {
                throw new ArgumentException("col is out of range", nameof(col));
            }

            string leftPart = currentLine.Substring(0, col);
            string rightPart = currentLine.Substring(col);

            string[] linesToInsert = s.Split(new string[] {Environment.NewLine}, StringSplitOptions.None);

            if (linesToInsert.Length == 1) {
                lines[ln] = leftPart + linesToInsert[0] + rightPart;
                return new Point(leftPart.Length + linesToInsert[0].Length, ln);
            } else {
                lines[ln] = leftPart + linesToInsert[0];
                lines.InsertRange(ln + 1, linesToInsert.Skip(1).Take(linesToInsert.Length - 1));
                lines[ln + linesToInsert.Length - 1] = lines[ln + linesToInsert.Length - 1] + rightPart;
                return new Point(lines[ln + linesToInsert.Length - 1].Length, ln + linesToInsert.Length - 1);
            }
        }

        /// <summary>
        /// Will write the content of text editor to matrix constrained with width/height,
        /// starting from (left, top) coord. Coords may be negative.
        /// If there are any gap before (or after) text due to margin, window will be filled
        /// with spaces there.
        /// Window size should be equal to width/height passed.
        /// </summary>
        public void WriteToWindow(int left, int top, int width, int height, char[,] window) {
            if (window.GetLength(0) != height) {
                throw new ArgumentException("window height differs from viewport height");
            }

            if (window.GetLength(1) != width) {
                throw new ArgumentException("window width differs from viewport width");
            }

            for (int y = top; y < 0; y++) {
                for (int x = 0; x < width; x++) {
                    window[y - top, x] = ' ';
                }
            }

            for (int y = Math.Max(0, top); y < Math.Min(top + height, lines.Count); y++) {
                string line = lines[y];
                for (int x = left; x < 0; x++) {
                    window[y - top, x - left] = ' ';
                }

                for (int x = Math.Max(0, left); x < Math.Min(left + width, line.Length); x++) {
                    window[y - top, x - left] = line[x];
                }

                for (int x = Math.Max(line.Length, left); x < left + width; x++) {
                    window[y - top, x - left] = ' ';
                }
            }

            for (int y = lines.Count; y < top + height; y++) {
                for (int x = 0; x < width; x++) {
                    window[y - top, x] = ' ';
                }
            }
        }

        public void Delete(int ln, int col, int count) {
            //
        }
    }


    /// <summary>
    /// Multiline text editor.
    /// </summary>
    [ContentProperty("Text")]
    public class TextEditor : Control {
        private TextEditorController controller;
        private char[,] buffer;

        public string Text {
            get => controller.Text;
            set {
                if (value != controller.Text) {
                    controller.Text = value;
                    invalidate();
                }
            }
        }

        private void invalidate() {
            CursorPosition = controller.CursorPos;
            Invalidate();
        }

        private void applyCommand(TextEditorController.ICommand cmd) {
            if (cmd.Do(controller)) {
                invalidate();
            }
        }

        public TextEditor() {
            controller = new TextEditorController("", 0, 0);
            KeyDown += OnKeyDown;
            MouseDown += OnMouseDown;
            CursorVisible = true;
            CursorPosition = new Point(0, 0);
            Focusable = true;
        }

        protected override Size MeasureOverride(Size availableSize) {
            return new Size(0, 0);
        }

        protected override Size ArrangeOverride(Size finalSize) {
            controller.Window = new Rect(controller.Window.TopLeft, finalSize);
            buffer = new char[finalSize.Height, finalSize.Width];
            return finalSize;
        }

        public override void Render(RenderingBuffer buffer) {
            var attrs = Colors.Blend(Color.Green, Color.DarkBlue);
            buffer.FillRectangle(0, 0, ActualWidth, ActualHeight, ' ', attrs);

            controller.WriteToWindow(this.buffer);
            for (int y = 0; y < ActualHeight; y++) {
                for (int x = 0; x < ActualWidth; x++) {
                    buffer.SetPixel(x, y, this.buffer[y, x]);
                }
            }
        }

        private void OnMouseDown(object sender, MouseButtonEventArgs mouseButtonEventArgs) {
            //
        }

        private void OnKeyDown(object sender, KeyEventArgs args) {
            ConsoleKeyInfo keyInfo = new ConsoleKeyInfo(args.UnicodeChar,
                (ConsoleKey) args.wVirtualKeyCode,
                (args.dwControlKeyState & ControlKeyState.SHIFT_PRESSED) == ControlKeyState.SHIFT_PRESSED,
                (args.dwControlKeyState & ControlKeyState.LEFT_ALT_PRESSED) == ControlKeyState.LEFT_ALT_PRESSED
                || (args.dwControlKeyState & ControlKeyState.RIGHT_ALT_PRESSED) == ControlKeyState.RIGHT_ALT_PRESSED,
                (args.dwControlKeyState & ControlKeyState.LEFT_CTRL_PRESSED) == ControlKeyState.LEFT_CTRL_PRESSED
                || (args.dwControlKeyState & ControlKeyState.RIGHT_CTRL_PRESSED) == ControlKeyState.RIGHT_CTRL_PRESSED
            );
            if (!char.IsControl(keyInfo.KeyChar)) {
                applyCommand(new TextEditorController.AppendStringCmd(new string(keyInfo.KeyChar, 1)));
            }

            if (keyInfo.Key == ConsoleKey.Enter) {
                applyCommand(new TextEditorController.AppendStringCmd("\n"));
            }
        }
    }
}
