using Microsoft.Data.SqlClient;
using System.Data;

namespace WeightDatabaseManager;

public sealed class DataGridViewActionColumn : DataGridViewColumn
{
    public DataGridViewActionColumn() : base(new DataGridViewActionCell())
    {
    }

    public override object Clone()
    {
        return base.Clone();
    }
}

public sealed class DataGridViewActionCell : DataGridViewCell
{
    public override Type EditType => null!;
    public override Type ValueType => typeof(string);
    public override object DefaultNewRowValue => string.Empty;
}

public class MainForm : Form
{
    private TextBox txtServer = null!;
    private ComboBox cboDatabase = null!;
    private Button btnConnect = null!;
    private Button btnRefresh = null!;
    private Button btnSaveAll = null!;
    private Label lblStatus = null!;
    private TabControl tabs = null!;
    private DataGridView gridWeightman = null!;
    private DataGridView gridWeightsave = null!;

    private SqlConnection? connection;

    // Original identity of rows marked for deletion.
    // DELETE is executed only when LƯU THAY ĐỔI is pressed.
    private readonly Dictionary<DataGridView, List<RowIdentity>> pendingDeletes = new();

    // Identity of rows currently in edit mode.
    private readonly Dictionary<DataGridView, HashSet<RowIdentity>> editingRows = new();

    // These are the actual SQL column names used by the existing database.
    private static readonly string[] DisplayColumns =
    {
        "ticketnum",
        "truckno",
        "custname",
        "Prodname",
        "Firstweight",
        "Secondweight",
        "date_in",
        "time_in"
    };

    private sealed record RowIdentity(string Ticketnum, string Truckno, DateTime DateIn);

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
            Text = "Kết nối SQL Server",
            Dock = DockStyle.Top,
            Height = 105,
            Padding = new Padding(14),
            Font = new Font("Segoe UI", 10F, FontStyle.Bold)
        };

        var lblServer = new Label
        {
            Text = "Server:",
            AutoSize = true,
            Left = 20,
            Top = 34,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold)
        };

        txtServer = new TextBox
        {
            Left = 80,
            Top = 29,
            Width = 240,
            Text = @".\SQLEXPRESS",
            Font = new Font("Segoe UI", 10F)
        };

        var lblDatabase = new Label
        {
            Text = "Database:",
            AutoSize = true,
            Left = 350,
            Top = 34,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold)
        };

        cboDatabase = new ComboBox
        {
            Left = 435,
            Top = 29,
            Width = 220,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Segoe UI", 10F)
        };

        btnConnect = new Button
        {
            Text = "Kết nối",
            Left = 680,
            Top = 26,
            Width = 115,
            Height = 34,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold)
        };
        btnConnect.Click += async (_, _) => await ConnectAsync();

        btnRefresh = new Button
        {
            Text = "Tải lại",
            Left = 805,
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

        pendingDeletes[gridWeightman] = new List<RowIdentity>();
        pendingDeletes[gridWeightsave] = new List<RowIdentity>();
        editingRows[gridWeightman] = new HashSet<RowIdentity>();
        editingRows[gridWeightsave] = new HashSet<RowIdentity>();

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
            Width = 180,
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

        var warning = new Label
        {
            Text = "Sửa/Xóa chỉ tác động database khi bạn bấm LƯU THAY ĐỔI.",
            AutoSize = true,
            Left = 215,
            Top = 25,
            ForeColor = Color.DimGray,
            Font = new Font("Segoe UI", 9F, FontStyle.Italic)
        };

        bottomPanel.Controls.AddRange(new Control[]
        {
            btnSaveAll, warning
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
        AddTextColumn(grid, "Số xe", "truckno", 125);
        AddTextColumn(grid, "Khách hàng", "custname", 220);
        AddTextColumn(grid, "Hàng hóa", "Prodname", 220);
        AddTextColumn(grid, "TL lần 1", "Firstweight", 125);
        AddTextColumn(grid, "TL lần 2", "Secondweight", 125);
        AddTextColumn(grid, "Ngày vào", "date_in", 125);
        AddTextColumn(grid, "Giờ vào", "time_in", 110);

        // One combined action column, matching the supplied design:
        // blue edit icon + red delete icon in the same Hành động column.
        var actionColumn = new DataGridViewActionColumn
        {
            Name = "Action",
            HeaderText = "Hành động",
            Width = 130,
            SortMode = DataGridViewColumnSortMode.NotSortable
        };
        grid.Columns.Add(actionColumn);

        grid.CellPainting += Grid_CellPainting;
        grid.CellMouseClick += Grid_CellMouseClick;
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
            grid.ClearSelection();
    }

    private void Grid_CellBeginEdit(object? sender, DataGridViewCellCancelEventArgs e)
    {
        if (sender is not DataGridView grid || e.RowIndex < 0)
            return;

        if (e.ColumnIndex == grid.Columns["ticketnum"]?.Index)
        {
            e.Cancel = true;
            return;
        }

        if (!IsRowEditing(grid.Rows[e.RowIndex]))
            e.Cancel = true;
    }

    private void Grid_CellEndEdit(object? sender, DataGridViewCellEventArgs e)
    {
        if (sender is DataGridView grid && e.RowIndex >= 0)
            UpdateSaveButtonState();
    }

    private void Grid_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        if (sender is not DataGridView grid || e.RowIndex < 0 || e.ColumnIndex < 0)
            return;

        if (grid.Columns[e.ColumnIndex].Name != "Action")
            return;

        e.PaintBackground(e.CellBounds, true);

        // Keep the normal cell border/background but draw two clean icons
        // side-by-side like the supplied dashboard design.
        var editRect = new Rectangle(
            e.CellBounds.Left + 18,
            e.CellBounds.Top + 7,
            42,
            e.CellBounds.Height - 14);

        var deleteRect = new Rectangle(
            e.CellBounds.Left + 72,
            e.CellBounds.Top + 7,
            42,
            e.CellBounds.Height - 14);

        using var editFont = new Font("Segoe MDL2 Assets", 19F);
        using var deleteFont = new Font("Segoe MDL2 Assets", 19F);

        TextRenderer.DrawText(
            e.Graphics,
            "\uE70F",
            editFont,
            editRect,
            Color.FromArgb(0, 88, 190),
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

        TextRenderer.DrawText(
            e.Graphics,
            "\uE74D",
            deleteFont,
            deleteRect,
            Color.Red,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

        e.Paint(e.ClipBounds, DataGridViewPaintParts.Border);
        e.Handled = true;
    }

    private async void Grid_CellMouseClick(object? sender, DataGridViewCellMouseEventArgs e)
    {
        if (sender is not DataGridView grid || e.RowIndex < 0 || e.ColumnIndex < 0)
            return;

        if (grid.Columns[e.ColumnIndex].Name != "Action")
            return;

        // The combined action cell is split into two clickable areas.
        int relativeX = e.X;

        if (relativeX >= 8 && relativeX < 65)
        {
            BeginRowEdit(grid, e.RowIndex);
        }
        else if (relativeX >= 65 && relativeX < 125)
        {
            await MarkRowForDeleteAsync(grid, e.RowIndex);
        }
    }

    private void BeginRowEdit(DataGridView grid, int rowIndex)
    {
        var row = grid.Rows[rowIndex];
        var identity = GetIdentity(row);

        if (identity == null)
        {
            MessageBox.Show(
                this,
                "Không xác định được Ticketnum + Số xe + Ngày vào của dòng này.",
                "Không thể sửa",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        editingRows[grid].Add(identity);

        // ticketnum, truckno and date_in are identity fields and remain read-only.
        foreach (DataGridViewCell cell in row.Cells)
            cell.ReadOnly = true;

        row.Cells["custname"].ReadOnly = false;
        row.Cells["Prodname"].ReadOnly = false;
        row.Cells["Firstweight"].ReadOnly = false;
        row.Cells["Secondweight"].ReadOnly = false;
        row.Cells["time_in"].ReadOnly = false;

        row.DefaultCellStyle.BackColor = Color.FromArgb(242, 235, 255);
        row.DefaultCellStyle.SelectionBackColor = Color.FromArgb(225, 210, 255);
        row.DefaultCellStyle.SelectionForeColor = Color.Black;

        grid.ClearSelection();
        row.Selected = true;
        grid.CurrentCell = row.Cells["custname"];
        grid.BeginEdit(true);

        UpdateSaveButtonState();
    }

    private async Task MarkRowForDeleteAsync(DataGridView grid, int rowIndex)
    {
        var row = grid.Rows[rowIndex];
        var identity = GetIdentity(row);

        if (identity == null)
            return;

        var result = MessageBox.Show(
            this,
            $"Bạn có chắc muốn xóa dòng:\n\n" +
            $"STT: {identity.Ticketnum}\n" +
            $"Số xe: {identity.Truckno}\n" +
            $"Ngày vào: {identity.DateIn:yyyy-MM-dd}\n\n" +
            "Dòng chỉ bị đánh dấu xóa trên màn hình.\n" +
            "Database chỉ xóa khi bạn bấm LƯU THAY ĐỔI.",
            "Xác nhận xóa",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);

        if (result != DialogResult.Yes)
            return;

        pendingDeletes[grid].Add(identity);
        editingRows[grid].Remove(identity);

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

    private static RowIdentity? GetIdentity(DataGridViewRow row)
    {
        try
        {
            var ticket = Convert.ToString(row.Cells["ticketnum"].Value)?.Trim() ?? "";
            var truck = Convert.ToString(row.Cells["truckno"].Value)?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(ticket) ||
                string.IsNullOrWhiteSpace(truck))
                return null;

            var dateValue = row.Cells["date_in"].Value;
            if (dateValue == null || dateValue == DBNull.Value)
                return null;

            if (!DateTime.TryParse(Convert.ToString(dateValue), out var dateIn))
                return null;

            return new RowIdentity(ticket, truck, dateIn.Date);
        }
        catch
        {
            return null;
        }
    }

    private bool IsRowEditing(DataGridViewRow row)
    {
        var identity = GetIdentity(row);

        return identity != null &&
               editingRows.Values.Any(set => set.Contains(identity));
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
                    string.Equals(x?.ToString(), "CANTIENPHAT", StringComparison.OrdinalIgnoreCase));

            if (preferred != null)
                cboDatabase.SelectedItem = preferred;
            else if (cboDatabase.Items.Count > 0)
                cboDatabase.SelectedIndex = 0;

            btnRefresh.Enabled = true;
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

        // DELETE using the original 3-part identity.
        foreach (var identity in pendingDeletes[grid])
        {
            using var deleteCmd = new SqlCommand(
                $@"DELETE FROM {tableName}
                   WHERE [ticketnum] = @ticketnum
                     AND [truckno] = @truckno
                     AND [date_in] = @date_in",
                connection);

            deleteCmd.Parameters.AddWithValue("@ticketnum", identity.Ticketnum);
            deleteCmd.Parameters.AddWithValue("@truckno", identity.Truckno);
            deleteCmd.Parameters.AddWithValue("@date_in", identity.DateIn);

            var affected = await deleteCmd.ExecuteNonQueryAsync();

            if (affected != 1)
            {
                throw new InvalidOperationException(
                    $"Không thể xác định chính xác 1 dòng để xóa.\n" +
                    $"Ticketnum: {identity.Ticketnum}\n" +
                    $"Truckno: {identity.Truckno}\n" +
                    $"Date_in: {identity.DateIn:yyyy-MM-dd}\n" +
                    $"Số dòng bị ảnh hưởng: {affected}");
            }
        }

        if (grid.DataSource is not DataTable table)
            return;

        // UPDATE only modified rows.
        // Identity fields ticketnum/truckno/date_in are not editable.
        foreach (DataRow row in table.Rows)
        {
            if (row.RowState == DataRowState.Deleted ||
                row.RowState != DataRowState.Modified)
                continue;

            var identity = GetIdentityFromDataRow(row);

            if (identity == null)
            {
                throw new InvalidOperationException(
                    "Không xác định được Ticketnum + Truckno + Date_in của một dòng đang sửa.");
            }

            using var updateCmd = new SqlCommand(
                $@"UPDATE {tableName}
                   SET [custname] = @custname,
                       [Prodname] = @Prodname,
                       [Firstweight] = @Firstweight,
                       [Secondweight] = @Secondweight,
                       [time_in] = @time_in
                   WHERE [ticketnum] = @ticketnum
                     AND [truckno] = @truckno
                     AND [date_in] = @date_in",
                connection);

            AddParameter(updateCmd, "@custname", row["custname"]);
            AddParameter(updateCmd, "@Prodname", row["Prodname"]);
            AddParameter(updateCmd, "@Firstweight", row["Firstweight"]);
            AddParameter(updateCmd, "@Secondweight", row["Secondweight"]);
            AddParameter(updateCmd, "@time_in", row["time_in"]);

            updateCmd.Parameters.AddWithValue("@ticketnum", identity.Ticketnum);
            updateCmd.Parameters.AddWithValue("@truckno", identity.Truckno);
            updateCmd.Parameters.AddWithValue("@date_in", identity.DateIn);

            var affected = await updateCmd.ExecuteNonQueryAsync();

            if (affected != 1)
            {
                throw new InvalidOperationException(
                    $"Không thể xác định chính xác 1 dòng để cập nhật.\n" +
                    $"Ticketnum: {identity.Ticketnum}\n" +
                    $"Truckno: {identity.Truckno}\n" +
                    $"Date_in: {identity.DateIn:yyyy-MM-dd}\n" +
                    $"Số dòng bị ảnh hưởng: {affected}");
            }
        }
    }

    private static RowIdentity? GetIdentityFromDataRow(DataRow row)
    {
        try
        {
            var ticket = Convert.ToString(row["ticketnum"])?.Trim() ?? "";
            var truck = Convert.ToString(row["truckno"])?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(ticket) ||
                string.IsNullOrWhiteSpace(truck))
                return null;

            var dateValue = row["date_in"];

            if (dateValue == null || dateValue == DBNull.Value)
                return null;

            if (!DateTime.TryParse(Convert.ToString(dateValue), out var dateIn))
                return null;

            return new RowIdentity(ticket, truck, dateIn.Date);
        }
        catch
        {
            return null;
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

    protected override async void OnFormClosed(FormClosedEventArgs e)
    {
        if (connection != null)
            await connection.DisposeAsync();

        base.OnFormClosed(e);
    }
}
