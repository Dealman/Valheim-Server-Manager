using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace Valheim_Server_Manager
{
    // Struct containing data pertaining the selected text
    struct Selection
    {
        public TextPointer Start;
        public TextPointer End;
        public TextRange Range;
        public string Selected;
    }

    // Enum used to decide which state the selection process is currently in
    enum SelectionState
    {
        None,
        Selecting,
        Selected
    }

    public partial class SelectableTextBlock : TextBlock
    {
        // Setup selection struct and initial state
        Selection selection = new Selection();
        SelectionState state = SelectionState.None;
        private string previousSelection;

        // Setup events
        public delegate void TextSelectedHandler(string SelectedText);
        public event TextSelectedHandler TextSelected;

        // In case we still want to use default behaviour for some reason
        public bool DefaultBehaviour = false;

        // Public property to get the currently selected text
        public string SelectedText { get { return selection.Selected; } }

        #region Cursor Update
        // Upon entering the TextBlock, change cursor to IBeam to indicate the text can be manipulated
        protected override void OnMouseEnter(MouseEventArgs e)
        {
            base.OnMouseEnter(e);

            Cursor = Cursors.IBeam;
        }

        // Upon leaving the TextBlock, revert back to Arrow
        protected override void OnMouseLeave(MouseEventArgs e)
        {
            base.OnMouseLeave(e);

            Cursor = Cursors.Arrow;
        }
        #endregion

        #region Mouse Button Events
        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            if (DefaultBehaviour)
                base.OnMouseLeftButtonDown(e);

            // If we already have some selected text, deselect it (TODO: Kinda buggy, need a better solution)
            if (state == SelectionState.Selected)
            {
                DeselectAll();
                state = SelectionState.None;
                e.Handled = true;
            }

            // Get the position, since this is at the beginning of the selection process - it'll be the start
            Point mousePos = e.GetPosition(this);
            selection.Start = this.GetPositionFromPoint(mousePos, true);
            // Update the state since we're now in the process of selecting text
            state = SelectionState.Selecting;
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            if (DefaultBehaviour)
                base.OnMouseLeftButtonUp(e);

            // Verify that we actually selected something before updating the state
            if (!String.IsNullOrEmpty(selection.Selected))
            {
                state = SelectionState.Selected;
                // If the event is bound, we pass it the currently selected text
                // we also make sure it's not the same text as last time
                if (selection.Selected != previousSelection)
                {
                    TextSelected?.Invoke(selection.Selected);
                }
                previousSelection = selection.Selected;
            } else {
                state = SelectionState.None;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (DefaultBehaviour)
                base.OnMouseMove(e);

            // Only run if the left-mouse button is pressed
            if (e.LeftButton == MouseButtonState.Pressed && state == SelectionState.Selecting)
            {
                // This is specific to MahApps Metro, ignore or replace with your own
                var accentColor = this.TryFindResource("MahApps.Brushes.Accent") as SolidColorBrush;

                // Fall back to some default colour if the above failed
                if (accentColor == null)
                    accentColor = new SolidColorBrush(Color.FromArgb(175, 30, 130, 220));

                // Revert any previous selection we had to transparent
                if (selection.Range != null && !selection.Range.IsEmpty)
                    selection.Range.ApplyPropertyValue(TextElement.BackgroundProperty, Brushes.Transparent);

                Point mousePos = e.GetPosition(this);
                selection.End = this.GetPositionFromPoint(mousePos, true);

                // Now that we have a start and end position, select the text inside and update the background
                selection.Range = new TextRange(selection.Start, selection.End);
                selection.Range.ApplyPropertyValue(TextElement.BackgroundProperty, accentColor);
                selection.Selected = selection.Range.Text;
            }
        }
        #endregion

        #region Public Methods
        public void SelectAll()
        {
            var accentColor = this.TryFindResource("MahApps.Brushes.Accent") as SolidColorBrush;

            if (accentColor == null)
                accentColor = new SolidColorBrush(Color.FromArgb(175, 30, 130, 220));

            selection.Start = this.ContentStart;
            selection.End = this.ContentEnd;
            selection.Range = new TextRange(selection.Start, selection.End);
            selection.Range.ApplyPropertyValue(TextElement.BackgroundProperty, accentColor);

            selection.Selected = selection.Range.Text;
            if (!(TextSelected == null) && !String.IsNullOrWhiteSpace(selection.Selected))
            {
                TextSelected(selection.Selected);
                state = SelectionState.Selected;
            }
        }

        public void DeselectAll()
        {
            if (selection.Range != null)
            {
                selection.Range.ApplyPropertyValue(TextElement.BackgroundProperty, Brushes.Transparent);
            } else {
                // In case for some reason there would be highlighted text, but nothing saved in Range - select all and revert it
                SelectAll();
                selection.Range.ApplyPropertyValue(TextElement.BackgroundProperty, Brushes.Transparent);
            }
        }

        public void CopyToClipboard()
        {
            if (selection.Selected == null)
                return;

            Clipboard.SetText(selection.Selected);
            //MessageBox.Show(selection.Selected);
        }

        public void Clear()
        {
            selection.Range = null;
            state = SelectionState.None;
            this.Inlines.Clear();
        }
        #endregion
    }
}
