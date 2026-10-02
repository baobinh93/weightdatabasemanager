using Microsoft.Data.SqlClient;
using System.Data;

namespace WeightDatabaseManager;

public sealed class DataGridViewDeleteColumn : DataGridViewColumn
{
    public DataGridViewDeleteColumn() : base(new DataGridViewDeleteCell())
    {
    }

    public override object Clone() => base.Clone();
}

public sealed class DataGridViewDeleteCell : DataGridViewCell
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
    private Button btnTruncate = null!;
    private Label lblStatus = null!;
    private TabControl tabs = null!;
    private DataGridView gridWeightman = null!;
    private DataGridView gridWeightsave = null!;

    private SqlConnection? connection;

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

    // Ticketnum + Truckno + Date_in is the row identity verified for this database.
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
            Text = "🔗  Kết nối",
            Left = 700,
            Top = 26,
            Width = 115,
            Height = 34,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold)
        };
        btnConnect.Click += async (_, _) => await ConnectAsync();

        btnRefresh = new Button
        {
            Text = "⟳  Tải lại",
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

        tabWeightman.Controls.Add(gridWeightman);
        tabWeightsave.Controls.Add(gridWeightsave);

        tabs.TabPages.Add(tabWeightman);
        tabs.TabPages.Add(tabWeightsave);

        var bottomPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 105,
            Padding = new Padding(12)
        };

        btnTruncate = new Button
        {
            Text = "✖  Xóa dữ liệu",
            Width = 190,
            Height = 48,
            Anchor = AnchorStyles.Right | AnchorStyles.Top,
            BackColor = Color.FromArgb(235, 35, 45),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 11F, FontStyle.Bold)
        };
        btnTruncate.FlatAppearance.BorderSize = 0;
        btnTruncate.Left = 1250;
        btnTruncate.Top = 8;
        btnTruncate.Click += async (_, _) => await TruncateBothTablesAsync();

        var warning = new Label
        {
            Text = "Xóa dữ liệu sẽ xóa toàn bộ dữ liệu",
            AutoSize = true,
            Anchor = AnchorStyles.Right | AnchorStyles.Top,
            ForeColor = Color.DimGray,
            Font = new Font("Segoe UI", 9F, FontStyle.Italic)
        };
        warning.Left = 1225;
        warning.Top = 62;

        bottomPanel.Controls.Add(btnTruncate);
        bottomPanel.Controls.Add(warning);

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
            ReadOnly = false,
            EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2,
            SelectionMode = DataGridViewSelectionMode.CellSelect,
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
                SelectionBackColor = Color.FromArgb(242, 235, 255),
                SelectionForeColor = Color.Black
            },
            AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Color.FromArgb(250, 247, 255),
                SelectionBackColor = Color.FromArgb(242, 235, 255),
                SelectionForeColor = Color.Black
            },
            GridColor = Color.FromArgb(235, 226, 250)
        };

        AddTextColumn(grid, "STT", "ticketnum", 75, true);
        AddTextColumn(grid, "Số xe", "truckno", 125, true);
        AddTextColumn(grid, "Khách hàng", "custname", 220, false);
        AddTextColumn(grid, "Hàng hóa", "Prodname", 220, false);
        AddTextColumn(grid, "TL lần 1", "Firstweight", 125, false);
        AddTextColumn(grid, "TL lần 2", "Secondweight", 125, false);
        AddTextColumn(grid, "Ngày vào", "date_in", 125, true);
        AddTextColumn(grid, "Giờ vào", "time_in", 110, false);

        var deleteColumn = new DataGridViewDeleteColumn
        {
            Name = "DeleteAction",
            HeaderText = "Hành động",
            Width = 100,
            SortMode = DataGridViewColumnSortMode.NotSortable,
            ReadOnly = true
        };
        grid.Columns.Add(deleteColumn);

        grid.CellPainting += Grid_CellPainting;
        grid.CellMouseClick += Grid_CellMouseClick;
        grid.CellEndEdit += Grid_CellEndEdit;
        grid.DataBindingComplete += (_, _) => ConfigureGridAfterLoad(grid);
        grid.DataError += Grid_DataError;

        return grid;
    }

    private static void AddTextColumn(
        DataGridView grid,
        string header,
        string propertyName,
        int width,
        bool readOnly)
    {
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = propertyName,
            HeaderText = header,
            DataPropertyName = propertyName,
            Width = width,
            SortMode = DataGridViewColumnSortMode.NotSortable,
            ReadOnly = readOnly
        });
    }

    private void ConfigureGridAfterLoad(DataGridView grid)
    {
        foreach (DataGridViewRow row in grid.Rows)
        {
            row.Cells["ticketnum"].ReadOnly = true;
            row.Cells["truckno"].ReadOnly = true;
            row.Cells["date_in"].ReadOnly = true;
            row.Cells["DeleteAction"].ReadOnly = true;
        }

        grid.ClearSelection();
    }

    private void Grid_DataError(object? sender, DataGridViewDataErrorEventArgs e)
    {
        e.ThrowException = false;
        MessageBox.Show(
            this,
            "Giá trị nhập không đúng kiểu dữ liệu của cột.",
            "Dữ liệu không hợp lệ",
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
    }

    private void Grid_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        if (sender is not DataGridView grid || e.RowIndex < 0 || e.ColumnIndex < 0)
            return;

        if (grid.Columns[e.ColumnIndex].Name != "DeleteAction")
            return;

        e.PaintBackground(e.CellBounds, true);

        using var deleteFont = new Font("Segoe MDL2 Assets", 19F);

        TextRenderer.DrawText(
            e.Graphics,
            "\uE74D",
            deleteFont,
            e.CellBounds,
            Color.FromArgb(235, 35, 45),
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

        e.Paint(e.ClipBounds, DataGridViewPaintParts.Border);
        e.Handled = true;
    }

    private async void Grid_CellMouseClick(object? sender, DataGridViewCellMouseEventArgs e)
    {
        if (sender is not DataGridView grid || e.RowIndex < 0 || e.ColumnIndex < 0)
            return;

        if (grid.Columns[e.ColumnIndex].Name == "DeleteAction")
            await DeleteRowImmediatelyAsync(grid, e.RowIndex);
    }

    private async void Grid_CellEndEdit(object? sender, DataGridViewCellEventArgs e)
    {
        if (sender is not DataGridView grid || e.RowIndex < 0 || e.ColumnIndex < 0)
            return;

        var columnName = grid.Columns[e.ColumnIndex].Name;

        // Identity and action columns cannot be edited.
        if (columnName is "ticketnum" or "truckno" or "date_in" or "DeleteAction")
            return;

        await UpdateRowImmediatelyAsync(grid, e.RowIndex);
    }

    private static RowIdentity? GetIdentity(DataGridViewRow row)
    {
        try
        {
            var ticket = Convert.ToString(row.Cells["ticketnum"].Value)?.Trim() ?? "";
            var truck = Convert.ToString(row.Cells["truckno"].Value)?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(ticket) || string.IsNullOrWhiteSpace(truck))
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

    private async Task UpdateRowImmediatelyAsync(DataGridView grid, int rowIndex)
    {
        if (connection == null || rowIndex < 0 || rowIndex >= grid.Rows.Count)
            return;

        var row = grid.Rows[rowIndex];
        var identity = GetIdentity(row);

        if (identity == null)
        {
            MessageBox.Show(
                this,
                "Không xác định được Ticketnum + Số xe + Ngày vào của dòng này.",
                "Không thể lưu",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            await LoadBothTablesAsync();
            return;
        }

        var tableName = grid == gridWeightman ? "dbo.Weightman" : "dbo.Weightsave";

        try
        {
            grid.EndEdit();

            using var cmd = new SqlCommand(
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

            AddParameter(cmd, "@custname", row.Cells["custname"].Value);
            AddParameter(cmd, "@Prodname", row.Cells["Prodname"].Value);
            AddParameter(cmd, "@Firstweight", row.Cells["Firstweight"].Value);
            AddParameter(cmd, "@Secondweight", row.Cells["Secondweight"].Value);
            AddParameter(cmd, "@time_in", row.Cells["time_in"].Value);

            // Ticketnum is decimal in SQL Server. Passing the displayed value as decimal
            // avoids an implicit string comparison against a decimal column.
            if (!decimal.TryParse(identity.Ticketnum, out var ticketDecimal))
                throw new InvalidOperationException($"Ticketnum không hợp lệ: {identity.Ticketnum}");

            cmd.Parameters.Add("@ticketnum", System.Data.SqlDbType.Decimal).Value = ticketDecimal;
            cmd.Parameters.Add("@truckno", System.Data.SqlDbType.NVarChar, 255).Value = identity.Truckno;
            cmd.Parameters.Add("@date_in", System.Data.SqlDbType.Date).Value = identity.DateIn;

            var affected = await cmd.ExecuteNonQueryAsync();

            if (affected != 1)
            {
                throw new InvalidOperationException(
                    $"Không thể xác định chính xác 1 dòng để cập nhật.\n" +
                    $"Ticketnum: {identity.Ticketnum}\n" +
                    $"Truckno: {identity.Truckno}\n" +
                    $"Date_in: {identity.DateIn:yyyy-MM-dd}\n" +
                    $"Số dòng bị ảnh hưởng: {affected}");
            }

            lblStatus.Text = $"Đã lưu thay đổi: STT {identity.Ticketnum} - {identity.Truckno}";
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                "Không thể lưu thay đổi của dòng này.\n\n" + ex.Message,
                "Save error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);

            await LoadBothTablesAsync();
        }
    }

    private async Task DeleteRowImmediatelyAsync(DataGridView grid, int rowIndex)
    {
        if (connection == null || rowIndex < 0 || rowIndex >= grid.Rows.Count)
            return;

        var row = grid.Rows[rowIndex];
        var identity = GetIdentity(row);

        if (identity == null)
            return;

        var result = MessageBox.Show(
            this,
            $"Bạn có chắc muốn xóa dòng này?\n\n" +
            $"STT: {identity.Ticketnum}\n" +
            $"Số xe: {identity.Truckno}\n" +
            $"Ngày vào: {identity.DateIn:yyyy-MM-dd}\n\n" +
            "Dòng sẽ bị xóa trực tiếp khỏi database.",
            "Xác nhận xóa",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);

        if (result != DialogResult.Yes)
            return;

        var tableName = grid == gridWeightman ? "dbo.Weightman" : "dbo.Weightsave";

        try
        {
            using var cmd = new SqlCommand(
                $@"DELETE FROM {tableName}
                   WHERE [ticketnum] = @ticketnum
                     AND [truckno] = @truckno
                     AND [date_in] = @date_in",
                connection);

            if (!decimal.TryParse(identity.Ticketnum, out var ticketDecimal))
                throw new InvalidOperationException($"Ticketnum không hợp lệ: {identity.Ticketnum}");

            cmd.Parameters.Add("@ticketnum", System.Data.SqlDbType.Decimal).Value = ticketDecimal;
            cmd.Parameters.Add("@truckno", System.Data.SqlDbType.NVarChar, 255).Value = identity.Truckno;
            cmd.Parameters.Add("@date_in", System.Data.SqlDbType.Date).Value = identity.DateIn;

            var affected = await cmd.ExecuteNonQueryAsync();

            if (affected != 1)
            {
                throw new InvalidOperationException(
                    $"Không thể xác định chính xác 1 dòng để xóa. Số dòng bị ảnh hưởng: {affected}");
            }

            await LoadBothTablesAsync();
            lblStatus.Text = $"Đã xóa: STT {identity.Ticketnum} - {identity.Truckno}";
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                "Không thể xóa dòng.\n\n" + ex.Message,
                "Delete error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private static void AddParameter(SqlCommand cmd, string name, object? value)
    {
        cmd.Parameters.AddWithValue(
            name,
            value == null || value == DBNull.Value ? DBNull.Value : value);
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
                .FirstOrDefault(x => string.Equals(
                    x?.ToString(), "CANTIENPHAT", StringComparison.OrdinalIgnoreCase));

            if (preferred != null)
                cboDatabase.SelectedItem = preferred;
            else if (cboDatabase.Items.Count > 0)
                cboDatabase.SelectedIndex = 0;

            btnRefresh.Enabled = true;
            btnTruncate.Enabled = true;
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
            var weightmanTable = await LoadTableAsync("dbo.Weightman");
            var weightsaveTable = await LoadTableAsync("dbo.Weightsave");

            gridWeightman.DataSource = weightmanTable;
            gridWeightsave.DataSource = weightsaveTable;

            gridWeightman.ClearSelection();
            gridWeightsave.ClearSelection();

            lblStatus.Text = $"Đã tải dữ liệu: {cboDatabase.SelectedItem}";
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

        await Task.Run(() => adapter.Fill(dt));
        return dt;
    }

    private async Task TruncateBothTablesAsync()
    {
        if (connection == null)
            return;

        var database = cboDatabase.SelectedItem?.ToString() ?? "";

        var result = MessageBox.Show(
            this,
            $"CẢNH BÁO!\n\n" +
            $"Bạn sắp XÓA TOÀN BỘ dữ liệu trong database '{database}':\n\n" +
            "• dbo.Weightman\n" +
            "• dbo.Weightsave\n\n" +
            "Thao tác này dùng TRUNCATE TABLE và sẽ xóa toàn bộ dữ liệu.\n" +
            "Không thể hoàn tác.\n\n" +
            "Bạn có chắc chắn muốn tiếp tục?",
            "XÓA TOÀN BỘ DỮ LIỆU",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);

        if (result != DialogResult.Yes)
            return;

        // Extra confirmation because this button destroys both tables.
        var result2 = MessageBox.Show(
            this,
            "Xác nhận lần cuối:\n\nTRUNCATE Weightman + Weightsave?",
            "Xác nhận lần cuối",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Stop,
            MessageBoxDefaultButton.Button2);

        if (result2 != DialogResult.Yes)
            return;

        try
        {
            btnTruncate.Enabled = false;

            using var transaction = await connection.BeginTransactionAsync();

            try
            {
                // The existing application design explicitly requests TRUNCATE on both tables.
                using var cmd = new SqlCommand(
                    "TRUNCATE TABLE dbo.Weightsave; TRUNCATE TABLE dbo.Weightman;",
                    connection,
                    (SqlTransaction)transaction);

                await cmd.ExecuteNonQueryAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }

            await LoadBothTablesAsync();

            MessageBox.Show(
                this,
                "Đã xóa toàn bộ dữ liệu của Weightman và Weightsave.",
                "Hoàn tất",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                "Không thể TRUNCATE 2 bảng.\n\n" + ex.Message,
                "Truncate error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            btnTruncate.Enabled = connection != null;
        }
    }

    protected override async void OnFormClosed(FormClosedEventArgs e)
    {
        if (connection != null)
            await connection.DisposeAsync();

        base.OnFormClosed(e);
    }
}
