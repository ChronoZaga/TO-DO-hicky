using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace ToDoHicky
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private ObservableCollection<TaskItem> _tasks = new ObservableCollection<TaskItem>();
        private CollectionViewSource _filteredTasksView;
        private readonly string _csvFilePath = "tasks.csv";
        private int _nextTaskId = 1;
        private bool _hideCompleted;
        private bool _hasSelectedTask;

        public ObservableCollection<TaskItem> Tasks
        {
            get => _tasks;
            set
            {
                _tasks = value;
                OnPropertyChanged(nameof(Tasks));
                if (_filteredTasksView != null)
                {
                    _filteredTasksView.Source = value;
                    _filteredTasksView.View?.Refresh();
                }
            }
        }

        public CollectionViewSource FilteredTasks
        {
            get => _filteredTasksView;
            set
            {
                _filteredTasksView = value;
                OnPropertyChanged(nameof(FilteredTasks));
            }
        }

        public bool HideCompleted
        {
            get => _hideCompleted;
            set
            {
                _hideCompleted = value;
                OnPropertyChanged(nameof(HideCompleted));
                _filteredTasksView.View?.Refresh();
            }
        }

        public bool HasSelectedTask
        {
            get => _hasSelectedTask;
            set
            {
                _hasSelectedTask = value;
                OnPropertyChanged(nameof(HasSelectedTask));
            }
        }

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
            _filteredTasksView = new CollectionViewSource { Source = Tasks };
            _filteredTasksView.Filter += ApplyFilter;
            LoadTasksFromCsv();
        }

        private void AddTask_Click(object sender, RoutedEventArgs e)
        {
            Tasks.Add(new TaskItem
            {
                TaskID = _nextTaskId++,
                TaskName = "New Task",
                Status = "Not Started",
                AssignedUser = Environment.UserName,
                DueDate = null,
                Notes = "",
                Priority = "Medium"
            });
            _filteredTasksView.View?.Refresh();
        }

        private void SaveTasks_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SaveTasksToCsv();
                StatusText.Text = "Tasks saved successfully.";
            }
            catch (IOException ex)
            {
                StatusText.Text = $"Error saving tasks: {ex.Message}";
            }
        }

        private void TasksGrid_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete && TasksGrid.SelectedItem is TaskItem selectedTask)
            {
                var result = MessageBox.Show($"Are you sure you want to delete the task '{selectedTask.TaskName}'?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    Tasks.Remove(selectedTask);
                    StatusText.Text = "Task deleted successfully.";
                    _filteredTasksView.View?.Refresh();
                }
                e.Handled = true;
            }
        }

        private void TasksGrid_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var hit = VisualTreeHelper.HitTest(TasksGrid, e.GetPosition(TasksGrid));
            if (hit != null && !(hit.VisualHit is DataGridRow))
            {
                TasksGrid.SelectedItem = null;
                HasSelectedTask = false;
                StatusText.Text = "Selection cleared.";
            }
        }

        private void TasksGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            HasSelectedTask = TasksGrid.SelectedItem != null;
        }

        private void HideCompletedCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            HideCompleted = true;
        }

        private void HideCompletedCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            HideCompleted = false;
        }

        private void ApplyFilter(object sender, FilterEventArgs e)
        {
            if (e.Item is TaskItem task)
            {
                e.Accepted = !HideCompleted || task.Status != "Completed";
            }
            else
            {
                e.Accepted = false;
            }
        }

        private void LoadTasksFromCsv()
        {
            if (!File.Exists(_csvFilePath))
            {
                StatusText.Text = $"CSV file not found at {_csvFilePath}";
                return;
            }

            try
            {
                var lines = File.ReadAllLines(_csvFilePath);
                if (lines.Length == 0)
                {
                    StatusText.Text = "CSV file is empty";
                    return;
                }

                var headers = ParseCsvLine(lines[0]);
                int idIndex = Array.IndexOf(headers, "TaskID");
                int taskIndex = Array.IndexOf(headers, "Task");
                int statusIndex = Array.IndexOf(headers, "Status");
                int userIndex = Array.IndexOf(headers, "AssignedUser");
                int dueDateIndex = Array.IndexOf(headers, "DueDate");
                int priorityIndex = Array.IndexOf(headers, "Priority");
                int notesIndex = Array.IndexOf(headers, "Notes");

                if (idIndex < 0 || taskIndex < 0 || statusIndex < 0 || userIndex < 0 || dueDateIndex < 0 || notesIndex < 0)
                {
                    StatusText.Text = "Missing required headers in CSV";
                    return;
                }

                Tasks.Clear();
                for (int i = 1; i < lines.Length; i++)
                {
                    var fields = ParseCsvLine(lines[i]);
                    int maxIndex = new[] { idIndex, taskIndex, statusIndex, userIndex, dueDateIndex, priorityIndex, notesIndex }.Max();
                    if (fields.Length > maxIndex)
                    {
                        if (int.TryParse(fields[idIndex].Trim('"'), out int taskId))
                        {
                            DateTime? dueDate = null;
                            string dueDateStr = fields[dueDateIndex].Trim('"');
                            if (!string.IsNullOrEmpty(dueDateStr))
                            {
                                if (DateTime.TryParseExact(dueDateStr, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out DateTime parsedDate))
                                {
                                    dueDate = parsedDate;
                                }
                                else
                                {
                                    StatusText.Text = $"Invalid DueDate format in row {i + 1}: {dueDateStr}";
                                    continue;
                                }
                            }

                            string status = fields[statusIndex].Trim('"');
                            if (!new[] { "Not Started", "In Progress", "Completed" }.Contains(status))
                            {
                                status = "Not Started";
                                StatusText.Text = $"Invalid Status in row {i + 1}: {fields[statusIndex]}, defaulting to Not Started";
                            }

                            string priority = priorityIndex >= 0 ? fields[priorityIndex].Trim('"') : "Medium";
                            if (!new[] { "Low", "Medium", "High", "URGENT" }.Contains(priority))
                            {
                                priority = "Medium";
                                StatusText.Text = $"Invalid Priority in row {i + 1}: {fields[priorityIndex]}, defaulting to Medium";
                            }

                            string notes = notesIndex >= 0 ? fields[notesIndex].Trim('"') : "";

                            Tasks.Add(new TaskItem
                            {
                                TaskID = taskId,
                                TaskName = fields[taskIndex].Trim('"'),
                                Status = status,
                                AssignedUser = fields[userIndex].Trim('"'),
                                DueDate = dueDate,
                                Notes = notes,
                                Priority = priority
                            });
                            _nextTaskId = Math.Max(_nextTaskId, taskId + 1);
                        }
                        else
                        {
                            StatusText.Text = $"Invalid TaskID in row {i + 1}: {fields[idIndex]}";
                        }
                    }
                    else
                    {
                        StatusText.Text = $"Row {i + 1} has too few fields: {string.Join(",", fields)}";
                    }
                }

                if (Tasks.Count == 0 && lines.Length > 1)
                {
                    StatusText.Text = "No valid tasks loaded. Check CSV format.";
                }
                else
                {
                    StatusText.Text = $"Loaded {Tasks.Count} tasks successfully.";
                }
            }
            catch (IOException ex)
            {
                StatusText.Text = $"Error loading tasks: {ex.Message}";
            }
        }

        private void SaveTasksToCsv()
        {
            var lines = new[] { "\"TaskID\",\"Task\",\"Status\",\"AssignedUser\",\"DueDate\",\"Priority\",\"Notes\"" }.Concat(
                Tasks.Select(t => $"\"{t.TaskID}\",\"{EscapeCsvField(t.TaskName)}\",\"{EscapeCsvField(t.Status)}\",\"{EscapeCsvField(t.AssignedUser)}\",\"{EscapeCsvField(t.DueDate?.ToString("yyyy-MM-dd") ?? "")}\",\"{EscapeCsvField(t.Priority)}\",\"{EscapeCsvField(t.Notes)}\"")
            );
            File.WriteAllLines(_csvFilePath, lines);
        }

        private string EscapeCsvField(string field)
        {
            if (string.IsNullOrEmpty(field)) return "";
            return field.Replace("\"", "\"\"");
        }

        private string[] ParseCsvLine(string line)
        {
            var fields = new List<string>();
            bool inQuotes = false;
            string field = "";
            bool fieldStarted = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (c == '"' && (i == 0 || line[i - 1] != '\\'))
                {
                    inQuotes = !inQuotes;
                    fieldStarted = true;
                    continue;
                }

                if (c == ',' && !inQuotes)
                {
                    fields.Add(field);
                    field = "";
                    fieldStarted = false;
                    continue;
                }

                field += c;
            }

            if (fieldStarted || !string.IsNullOrEmpty(field))
            {
                fields.Add(field);
            }

            return fields.ToArray();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class TaskItem : INotifyPropertyChanged
    {
        private int _taskID;
        private string _taskName;
        private string _status;
        private string _assignedUser;
        private DateTime? _dueDate;
        private string _notes;
        private string _priority;

        public int TaskID
        {
            get => _taskID;
            set
            {
                _taskID = value;
                OnPropertyChanged(nameof(TaskID));
            }
        }

        public string TaskName
        {
            get => _taskName;
            set
            {
                _taskName = value;
                OnPropertyChanged(nameof(TaskName));
            }
        }

        public string Status
        {
            get => _status;
            set
            {
                _status = value;
                OnPropertyChanged(nameof(Status));
            }
        }

        public string AssignedUser
        {
            get => _assignedUser;
            set
            {
                _assignedUser = value;
                OnPropertyChanged(nameof(AssignedUser));
            }
        }

        public DateTime? DueDate
        {
            get => _dueDate;
            set
            {
                _dueDate = value;
                OnPropertyChanged(nameof(DueDate));
            }
        }

        public string Notes
        {
            get => _notes;
            set
            {
                _notes = value;
                OnPropertyChanged(nameof(Notes));
            }
        }

        public string Priority
        {
            get => _priority;
            set
            {
                _priority = value;
                OnPropertyChanged(nameof(Priority));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}