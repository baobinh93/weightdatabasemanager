using Microsoft.Data.SqlClient;
using System.Data;

namespace WeightDatabaseManager;

public class MainForm : Form
{
    private TextBox txtServer = null!;
    private ComboBox cboDatabase = null!;
    private Button btnConnect = null!;
    private Button btnRefresh = null!;
    private Button btnReset = null!;
    private Button btnSaveAll = null!;
    private Label lblStatus = null!;
    private TabControl tabs = null!;
    private DataGridView gridWeightman = null!;
    private DataGridView gridWeightsave = null!;

    private SqlConnection? connection;

    // Original ticket numbers of rows deleted by the user.
    // The actual DELETE is executed only when LƯU is pressed.
    private readonly Dictionary<DataGridView, HashSet<string>> pendingDeletes = new();

    // Ticket numbers of rows currently in edit mode.
    private readonly Dictionary<DataGridView, HashSet<string>> editingRows = new();

    private static readonly string[] DisplayColumns =
    {
        "ticketnum",
        "truckno",
        "custname",
        "Prodname",
        "Fistweight",
        "Secondweight",
        "date_in",
        "time_in"
    };

    public MainForm()
    {
        Text = "Weight Database Manager";
        Width = 1500;
        Height = 850;
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1100, 650);

        BuildUi();
    }

    private void BuildUi()
    {
        BackColor = Color.White;

        var connectionGroup = new GroupBox
        {
            Text = "Connect to server",
            Dock = DockStyle.Top,
            Height = 105,
            Padding = new Padding(14),
            Font = new Font("Segoe UI", 10F, FontStyle.Bold)
        };

        var lblServer = new Label
        {
            Text = "Server name:",
            AutoSize = true,
            Left = 20,
            Top = 34,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold)
        };

        txtServer = new TextBox
        {
            Left = 125,
            Top = 29,
            Width = 220,
            Text = @".\SQLEXPRESS",
            Font = new Font("Segoe UI", 10F)
        };

        var lblDatabase = new Label
        {
            Text = "Database:",
            AutoSize = true,
            Left = 380,
            Top = 34,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold)
        };

        cboDatabase = new ComboBox
        {
            Left = 455,
            Top = 29,
            Width = 220,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Segoe UI", 10F)
        };

        btnConnect = new Button
        {
            Text = "Kết nối",
            Left = 700,
            Top = 26,
            Width = 115,
            Height = 34,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold)
        };
        btnConnect.Click += async (_, _) => await ConnectAsync();

        btnRefresh = new Button
        {
            Text = "Tải lại",
            Left = 825,
            Top = 26,
            Width = 110,
            Height = 34,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            Enabled = false
        };
        btnRefresh.Click += async (_, _) => await LoadBothTablesAsync();

        lblStatus = new Label
        {
            Text = "Chưa kết nối",
            AutoSize = true,
            Left = 20,
            Top = 72,
            ForeColor = Color.DimGray,
            Font = new Font("Segoe UI", 9F)
        };

        connectionGroup.Controls.AddRange(new Control[]
        {
            lblServer, txtServer, lblDatabase, cboDatabase,
            btnConnect, btnRefresh, lblStatus
        });

        tabs = new TabControl
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 10F)
        };

        var tabWeightman = new TabPage("Xe trong ngày");
        var tabWeightsave = new TabPage("Xe đã lưu");

        gridWeightman = CreateGrid();
        gridWeightsave = CreateGrid();

        pendingDeletes[gridWeightman] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        pendingDeletes[gridWeightsave] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        editingRows[gridWeightman] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        editingRows[gridWeightsave] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        tabWeightman.Controls.Add(gridWeightman);
        tabWeightsave.Controls.Add(gridWeightsave);

        tabs.TabPages.Add(tabWeightman);
        tabs.TabPages.Add(tabWeightsave);

        var bottomPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 72,
            Padding = new Padding(12)
        };

        btnSaveAll = new Button
        {
            Text = "LƯU THAY ĐỔI",
            Width = 170,
            Height = 42,
            Left = 12,
            Top = 12,
            BackColor = Color.SeaGreen,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            Enabled = false
        };
        btnSaveAll.Click += async (_, _) => await SaveAllChangesAsync();

        btnReset = new Button
        {
            Text = "RESET",
            Width = 120,
            Height = 42,
            Left = 195,
            Top = 12,
            BackColor = Color.Firebrick,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            Enabled = false
        };
        btnReset.Click += async (_, _) => await ResetTablesAsync();

        var warning = new Label
        {
            Text = "Xóa/sửa chỉ thay đổi trên màn hình. Phải bấm LƯU THAY ĐỔI mới cập nhật database.",
            AutoSize = true,
            Left = 335,
            Top = 25,
            ForeColor = Color.DimGray,
            Font = new Font("Segoe UI", 9F, FontStyle.Italic)
        };

        bottomPanel.Controls.AddRange(new Control[]
        {
            btnSaveAll, btnReset, warning
        });

        Controls.Add(tabs);
        Controls.Add(bottomPanel);
        Controls.Add(connectionGroup);
    }

    private DataGridView CreateGrid()
    {
        var grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.None,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            AutoGenerateColumns = false,
            ReadOnly = true,
            EditMode = DataGridViewEditMode.EditProgrammatically,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            RowHeadersVisible = false,
            AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None,
            RowTemplate = { Height = 42 },
            EnableHeadersVisualStyles = false,
            ColumnHeadersHeight = 42,
            ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Color.FromArgb(238, 229, 255),
                ForeColor = Color.Black,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Alignment = DataGridViewContentAlignment.MiddleCenter
            },
            DefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Color.White,
                ForeColor = Color.Black,
                Font = new Font("Segoe UI", 9.5F),
                Alignment = DataGridViewContentAlignment.MiddleCenter,
                SelectionBackColor = Color.White,
                SelectionForeColor = Color.Black
            },
            AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Color.FromArgb(250, 247, 255),
                SelectionBackColor = Color.FromArgb(250, 247, 255),
                SelectionForeColor = Color.Black
            },
            GridColor = Color.FromArgb(235, 226, 250)
        };

        AddTextColumn(grid, "STT", "ticketnum", 75);
        AddTextColumn(grid, "Số xe", "truckno", 110);
        AddTextColumn(grid, "Khách hàng", "custname", 220);
        AddTextColumn(grid, "Hàng hóa", "Prodname", 220);
        AddTextColumn(grid, "TL lần 1", "Fistweight", 125);
        AddTextColumn(grid, "TL lần 2", "Secondweight", 125);
        AddTextColumn(grid, "Ngày vào", "date_in", 125);
        AddTextColumn(grid, "Giờ vào", "time_in", 110);

        var editButton = new DataGridViewButtonColumn
        {
            Name = "EditAction",
            HeaderText = "Sửa",
            Text = "Sửa",
            UseColumnTextForButtonValue = true,
            Width = 75,
            FlatStyle = FlatStyle.Flat
        };
        grid.Columns.Add(editButton);

        var deleteButton = new DataGridViewButtonColumn
        {
            Name = "DeleteAction",
            HeaderText = "Xóa",
            Text = "Xóa",
            UseColumnTextForButtonValue = true,
            Width = 75,
            FlatStyle = FlatStyle.Flat
        };
        grid.Columns.Add(deleteButton);

        grid.CellContentClick += Grid_CellContentClick;
        grid.CellBeginEdit += Grid_CellBeginEdit;
        grid.CellEndEdit += Grid_CellEndEdit;
        grid.SelectionChanged += Grid_SelectionChanged;
        grid.DataBindingComplete += (_, _) => ConfigureGridAfterLoad(grid);

        return grid;
    }

    private static void AddTextColumn(DataGridView grid, string header, string propertyName, int width)
    {
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = propertyName,
            HeaderText = header,
            DataPropertyName = propertyName,
            Width = width,
            SortMode = DataGridViewColumnSortMode.NotSortable,
            ReadOnly = true
        });
    }

    private void ConfigureGridAfterLoad(DataGridView grid)
    {
        foreach (DataGridViewRow row in grid.Rows)
        {
            row.DefaultCellStyle.SelectionBackColor = row.DefaultCellStyle.BackColor;
            row.DefaultCellStyle.SelectionForeColor = row.DefaultCellStyle.ForeColor;
        }

        grid.ClearSelection();
        UpdateSaveButtonState();
    }

    private void Grid_SelectionChanged(object? sender, EventArgs e)
    {
        if (sender is not DataGridView grid)
            return;

        if (grid.CurrentRow == null || !IsRowEditing(grid.CurrentRow))
        {
            grid.ClearSelection();
        }
    }

    private void Grid_CellBeginEdit(object? sender, DataGridViewCellCancelEventArgs e)
    {
        if (sender is not DataGridView grid || e.RowIndex < 0)
            return;

        if (e.ColumnIndex == grid.Columns["ticketnum"]?.Index)
            e.Cancel = true;

        if (!IsRowEditing(grid.Rows[e.RowIndex]))
            e.Cancel = true;
    }

    private void Grid_CellEndEdit(object? sender, DataGridViewCellEventArgs e)
    {
        if (sender is DataGridView grid && e.RowIndex >= 0)
        {
            UpdateSaveButtonState();
        }
    }

    private async void Grid_CellContentClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (sender is not DataGridView grid || e.RowIndex < 0 || e.ColumnIndex < 0)
            return;

        if (grid.Columns[e.ColumnIndex].Name == "EditAction")
        {
            BeginRowEdit(grid, e.RowIndex);
        }
        else if (grid.Columns[e.ColumnIndex].Name == "DeleteAction")
        {
            await MarkRowForDeleteAsync(grid, e.RowIndex);
        }
    }

    private void BeginRowEdit(DataGridView grid, int rowIndex)
    {
        var row = grid.Rows[rowIndex];
        var ticket = GetTicket(row);

        if (string.IsNullOrWhiteSpace(ticket))
        {
            MessageBox.Show(this, "Không xác định được ticketnum của dòng này.",
                "Không thể sửa", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        editingRows[grid].Add(ticket);

        // Only this row becomes editable. ticketnum remains the key and is read-only.
        foreach (DataGridViewCell cell in row.Cells)
            cell.ReadOnly = true;

        for (int i = 0; i < 8; i++)
            row.Cells[i].ReadOnly = i == 0;

        row.DefaultCellStyle.BackColor = Color.FromArgb(242, 235, 255);
        row.DefaultCellStyle.SelectionBackColor = Color.FromArgb(225, 210, 255);
        row.DefaultCellStyle.SelectionForeColor = Color.Black;

        grid.ClearSelection();
        row.Selected = true;
        grid.CurrentCell = row.Cells[1];
        grid.BeginEdit(true);

        UpdateSaveButtonState();
    }

    private async Task MarkRowForDeleteAsync(DataGridView grid, int rowIndex)
    {
        var row = grid.Rows[rowIndex];
        var ticket = GetTicket(row);

        if (string.IsNullOrWhiteSpace(ticket))
            return;

        var result = MessageBox.Show(
            this,
            $"Bạn có chắc muốn xóa xe có STT/ticketnum: {ticket}?\n\n" +
            "Dòng này sẽ chỉ bị xóa trên màn hình.\n" +
            "Bạn phải bấm LƯU THAY ĐỔI thì database mới bị xóa.",
            "Xác nhận xóa",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);

        if (result != DialogResult.Yes)
            return;

        // Do not execute SQL here. Remember the original ticket number,
        // remove the row from the in-memory table, and wait for LƯU.
        pendingDeletes[grid].Add(ticket);
        editingRows[grid].Remove(ticket);

        if (grid.DataSource is DataTable table)
        {
            DataRowView? rowView = row.DataBoundItem as DataRowView;
            if (rowView != null)
                rowView.Row.Delete();
            else
                grid.Rows.RemoveAt(rowIndex);
        }
        else
        {
            grid.Rows.RemoveAt(rowIndex);
        }

        grid.ClearSelection();
        UpdateSaveButtonState();

        await Task.CompletedTask;
    }

    private static string GetTicket(DataGridViewRow row)
    {
        if (row.Cells["ticketnum"].Value == null ||
            row.Cells["ticketnum"].Value == DBNull.Value)
            return "";

        return Convert.ToString(row.Cells["ticketnum"].Value)?.Trim() ?? "";
    }

    private bool IsRowEditing(DataGridViewRow row)
    {
        var ticket = GetTicket(row);
        return !string.IsNullOrWhiteSpace(ticket) &&
               editingRows.Values.Any(set => set.Contains(ticket));
    }

    private async Task ConnectAsync()
    {
        try
        {
            btnConnect.Enabled = false;
            lblStatus.Text = "Đang kết nối...";

            if (connection != null)
            {
                await connection.DisposeAsync();
                connection = null;
            }

            var builder = new SqlConnectionStringBuilder
            {
                DataSource = txtServer.Text.Trim(),
                InitialCatalog = "master",
                IntegratedSecurity = true,
                TrustServerCertificate = true,
                ConnectTimeout = 5
            };

            connection = new SqlConnection(builder.ConnectionString);
            await connection.OpenAsync();

            var dbs = new DataTable();
            using (var cmd = new SqlCommand(
                "SELECT name FROM sys.databases WHERE state_desc = 'ONLINE' ORDER BY name",
                connection))
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                dbs.Load(reader);
            }

            cboDatabase.Items.Clear();

            foreach (DataRow row in dbs.Rows)
                cboDatabase.Items.Add(row["name"].ToString());

            var preferred = cboDatabase.Items.Cast<object>()
                .FirstOrDefault(x =>
                    string.Equals(x?.ToString(), "CANTHUONG", StringComparison.OrdinalIgnoreCase));

            if (preferred != null)
                cboDatabase.SelectedItem = preferred;
            else if (cboDatabase.Items.Count > 0)
                cboDatabase.SelectedIndex = 0;

            btnRefresh.Enabled = true;
            btnReset.Enabled = true;
            lblStatus.Text = $"Đã kết nối: {txtServer.Text.Trim()}";

            cboDatabase.SelectedIndexChanged -= DatabaseChanged;
            cboDatabase.SelectedIndexChanged += DatabaseChanged;

            if (cboDatabase.SelectedItem != null)
                await SelectDatabaseAndLoadAsync(cboDatabase.SelectedItem.ToString()!);
        }
        catch (Exception ex)
        {
            lblStatus.Text = "Kết nối thất bại";
            MessageBox.Show(
                this,
                "Không thể kết nối SQL Server.\n\n" + ex.Message,
                "Connection error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            btnConnect.Enabled = true;
        }
    }

    private async void DatabaseChanged(object? sender, EventArgs e)
    {
        if (cboDatabase.SelectedItem is string db)
            await SelectDatabaseAndLoadAsync(db);
    }

    private async Task SelectDatabaseAndLoadAsync(string database)
    {
        if (connection == null)
            return;

        try
        {
            await connection.CloseAsync();

            var builder = new SqlConnectionStringBuilder(connection.ConnectionString)
            {
                InitialCatalog = database
            };

            await connection.DisposeAsync();
            connection = new SqlConnection(builder.ConnectionString);
            await connection.OpenAsync();

            await LoadBothTablesAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                ex.Message,
                "Database error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private async Task LoadBothTablesAsync()
    {
        if (connection == null)
            return;

        try
        {
            ClearPendingChanges();

            var weightmanTable = await LoadTableAsync("dbo.Weightman");
            var weightsaveTable = await LoadTableAsync("dbo.Weightsave");

            gridWeightman.DataSource = weightmanTable;
            gridWeightsave.DataSource = weightsaveTable;

            gridWeightman.ClearSelection();
            gridWeightsave.ClearSelection();

            lblStatus.Text = $"Đã tải dữ liệu: {cboDatabase.SelectedItem}";
            UpdateSaveButtonState();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                "Không thể đọc Weightman/Weightsave.\n\n" + ex.Message,
                "Load error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private async Task<DataTable> LoadTableAsync(string tableName)
    {
        var dt = new DataTable();

        var sql =
            $"SELECT [{DisplayColumns[0]}], [{DisplayColumns[1]}], [{DisplayColumns[2]}], " +
            $"[{DisplayColumns[3]}], [{DisplayColumns[4]}], [{DisplayColumns[5]}], " +
            $"[{DisplayColumns[6]}], [{DisplayColumns[7]}] FROM {tableName}";

        using var cmd = new SqlCommand(sql, connection);
        using var adapter = new SqlDataAdapter(cmd);

        // Needed if this DataTable is ever used with generated commands.
        adapter.MissingSchemaAction = MissingSchemaAction.AddWithKey;

        await Task.Run(() => adapter.Fill(dt));

        return dt;
    }

    private async Task SaveAllChangesAsync()
    {
        if (connection == null)
            return;

        try
        {
            btnSaveAll.Enabled = false;

            foreach (var grid in new[] { gridWeightman, gridWeightsave })
            {
                grid.EndEdit();

                var tableName = grid == gridWeightman
                    ? "dbo.Weightman"
                    : "dbo.Weightsave";

                await SaveGridChangesAsync(grid, tableName);
            }

            ClearPendingChanges();

            await LoadBothTablesAsync();

            MessageBox.Show(
                this,
                "Đã lưu tất cả thay đổi vào database.",
                "Lưu thành công",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                "Không thể lưu thay đổi.\n\n" + ex.Message,
                "Save error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            UpdateSaveButtonState();
        }
    }

    private async Task SaveGridChangesAsync(DataGridView grid, string tableName)
    {
        if (connection == null)
            return;

        var deletes = pendingDeletes[grid];

        // DELETE rows that were marked for deletion.
        foreach (var ticket in deletes)
        {
            using var deleteCmd = new SqlCommand(
                $"DELETE FROM {tableName} WHERE [ticketnum] = @ticketnum",
                connection);

            deleteCmd.Parameters.AddWithValue("@ticketnum", ticket);
            await deleteCmd.ExecuteNonQueryAsync();
        }

        if (grid.DataSource is not DataTable table)
            return;

        // Update rows currently in the DataTable.
        // ticketnum is treated as the stable key and is not changed.
        foreach (DataRow row in table.Rows)
        {
            if (row.RowState == DataRowState.Deleted)
                continue;

            if (row.RowState != DataRowState.Modified)
                continue;

            using var updateCmd = new SqlCommand(
                $@"UPDATE {tableName}
                   SET [truckno] = @truckno,
                       [custname] = @custname,
                       [Prodname] = @Prodname,
                       [Fistweight] = @Fistweight,
                       [Secondweight] = @Secondweight,
                       [date_in] = @date_in,
                       [time_in] = @time_in
                   WHERE [ticketnum] = @ticketnum",
                connection);

            AddParameter(updateCmd, "@truckno", row["truckno"]);
            AddParameter(updateCmd, "@custname", row["custname"]);
            AddParameter(updateCmd, "@Prodname", row["Prodname"]);
            AddParameter(updateCmd, "@Fistweight", row["Fistweight"]);
            AddParameter(updateCmd, "@Secondweight", row["Secondweight"]);
            AddParameter(updateCmd, "@date_in", row["date_in"]);
            AddParameter(updateCmd, "@time_in", row["time_in"]);
            AddParameter(updateCmd, "@ticketnum", row["ticketnum"]);

            await updateCmd.ExecuteNonQueryAsync();
        }
    }

    private static void AddParameter(SqlCommand cmd, string name, object? value)
    {
        cmd.Parameters.AddWithValue(
            name,
            value == null || value == DBNull.Value ? DBNull.Value : value);
    }

    private void UpdateSaveButtonState()
    {
        bool hasDeletes =
            pendingDeletes[gridWeightman].Count > 0 ||
            pendingDeletes[gridWeightsave].Count > 0;

        bool hasEditing =
            editingRows[gridWeightman].Count > 0 ||
            editingRows[gridWeightsave].Count > 0;

        bool hasModifiedData =
            HasModifiedRows(gridWeightman) ||
            HasModifiedRows(gridWeightsave);

        btnSaveAll.Enabled = hasDeletes || hasEditing || hasModifiedData;
    }

    private static bool HasModifiedRows(DataGridView grid)
    {
        if (grid.DataSource is not DataTable table)
            return false;

        return table.Rows.Cast<DataRow>()
            .Any(r => r.RowState == DataRowState.Modified);
    }

    private void ClearPendingChanges()
    {
        pendingDeletes[gridWeightman].Clear();
        pendingDeletes[gridWeightsave].Clear();
        editingRows[gridWeightman].Clear();
        editingRows[gridWeightsave].Clear();
    }

    private async Task ResetTablesAsync()
    {
        if (connection == null)
            return;

        var database = cboDatabase.SelectedItem?.ToString() ?? "";

        var result = MessageBox.Show(
            this,
            $"Bạn chắc chắn muốn RESET database '{database}'?\n\n" +
            "Toàn bộ dữ liệu trong:\n" +
            "• dbo.Weightman\n" +
            "• dbo.Weightsave\n\n" +
            "sẽ bị XÓA TOÀN BỘ bằng TRUNCATE TABLE.\n\n" +
            "Thao tác này không thể hoàn tác.",
            "CONFIRM RESET",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);

        if (result != DialogResult.Yes)
            return;

        try
        {
            btnReset.Enabled = false;

            using var transaction =
                (SqlTransaction)await connection.BeginTransactionAsync();

            try
            {
                using var cmd = new SqlCommand(
                    "TRUNCATE TABLE dbo.Weightsave; TRUNCATE TABLE dbo.Weightman;",
                    connection,
                    transaction);

                await cmd.ExecuteNonQueryAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }

            ClearPendingChanges();
            await LoadBothTablesAsync();

            MessageBox.Show(
                this,
                "Đã RESET thành công 2 bảng.",
                "RESET complete",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                "RESET không thực hiện được.\n\n" +
                "SQL Server có thể đang có FOREIGN KEY hoặc ràng buộc khiến TRUNCATE không được phép.\n\n" +
                ex.Message,
                "RESET error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            btnReset.Enabled = connection != null;
        }
    }

    protected override async void OnFormClosed(FormClosedEventArgs e)
    {
        if (connection != null)
            await connection.DisposeAsync();

        base.OnFormClosed(e);
    }
}
