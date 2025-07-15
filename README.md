<img width="1884" height="1710" alt="Screenshot (13)" src="https://github.com/user-attachments/assets/efa87c97-d52a-45df-b279-dfbd40941e32" />
"TO-DO-hicky" is a task management application built using WPF and .NET 8.0. It lets users create, manage, and track tasks with alphanumeric TaskIDs, priority settings, due dates, and status tracking. Tasks are stored in a CSV file, and the app has a user-friendly interface with heatmap-style coloring for task statuses.

FEATURES

Task Management: Create, edit, and delete tasks with fields for TaskID, Task Name, Status, Assigned User, Due Date, Priority, and Notes.

Alphanumeric TaskIDs: Assign tasks unique IDs with letters and numbers (e.g., 1A, 1-A) for sub-task organization, with new tasks getting the next sequential numeric ID.

Heatmap Coloring: Visual cues show task status: Completed (Sky Blue), Past Due (Red), Due Today (Yellow), Urgent Priority (Orange), In Progress (Light Green), Not Started (Light Gray).

Filtering: Hide completed tasks for a focused view.

Data Persistence: Tasks are saved to and loaded from a tasks.csv file.

Notes Editing: A textbox allows editing notes for the selected task, with single-line display in the DataGrid.


PREREQUISITES

Operating System: Windows (due to WPF and .NET 8.0-windows framework)
Disk Space: Minimal, for the app and CSV file storage

USAGE

1. Launching the App
Run the prebuilt TO-DO-hicky executable to open the main window, showing a DataGrid of tasks sorted by TaskID. If a tasks.csv file exists in the app’s directory, tasks load automatically.

2. Managing Tasks
Add Task: Click the Add Task button to create a task with the next numeric TaskID (e.g., 1, 2).
Edit Task: Modify task details (TaskID, Task Name, Status, Assigned User, Due Date, Priority, Notes) in the DataGrid or use the Notes textbox for multi-line notes.
Delete Task: Select a task, press Delete, then confirm to remove it.
Hide Completed Tasks: Check the Hide Completed Tasks checkbox to filter out completed tasks.

3. Saving Tasks
Click the Save button to save tasks to tasks.csv in the app’s directory. Closing the app with unsaved changes prompts to save, discard, or cancel.

4. TaskID Customization
Edit TaskIDs in the DataGrid to use alphanumeric values (e.g., 1A, 1-A) for sub-tasks. TaskIDs must be unique and non-empty; invalid entries show an error in the status bar.

5. CSV File Format
The tasks.csv file uses the header: TaskID,Task,Status,AssignedUser,DueDate,Priority,Notes.
Example row: "1A","Complete report","In Progress","User1","2025-07-20","High","Review final draft".
Fields are quoted, and double quotes within fields are escaped as two single quotes ('').

LICENSE

This project is licensed under the MIT License.
