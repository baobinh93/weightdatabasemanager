using Microsoft.Data.SqlClient;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace WeightDatabaseManager;

public class MainForm : Form
{
    private TextBox txtServer = null!;
    private ComboBox cboDatabase = null!;
    private Button btnConnect = null!;
    private Button btnRefresh = null!;
    private Button btnDeleteAll = null!;
    private Label lblStatus = null!;
    private TabControl tabs = null!;
    private DataGridView gridWeightman = null!;
    private DataGridView gridWeightsave = null!;

    private SqlConnection? connection;

    // Các cột được phép sửa trực tiếp.
    private static readonly HashSet<string> EditableColumns =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "custname",
            "Prodname",
            "Firstweight",
            "Secondweight",
            "time_in"
        };

    // Ba trường này được dùng để xác định chính xác một record.
    private static readonly string[] KeyColumns =
    {
        "ticketnum",
        "truckno",
        "date_in"
    };

    // Chỉ lấy những cột cần hiển thị trên dashboard.
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

    public MainForm()
    {
        Text = "Weight Database Manager";
        Width = 1600;
        Height = 900;
        MinimumSize = new Size(1200, 700);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.White;
        Font = new Font("Segoe UI", 10F);

        BuildUi();
    }

    private void BuildUi()
    {
        var root = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Padding = new Padding(28, 20, 28, 20)
        };

        // ===== CONNECT TO SERVER =====
        var connectionGroup = new GroupBox
        {
            Text = "Connect to server",
            Dock = DockStyle.Top,
            Height = 120,
            Padding = new Padding(18, 24, 18, 12),
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            ForeColor = Color.Black
        };

        var connectionArea = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(243, 235, 255)
        };

        var lblServer = new Label
        {
            Text = "Server name:",
            AutoSize = true,
            Left = 28,
            Top = 25,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold)
        };

        txtServer = new TextBox
        {
            Left = 145,
            Top = 19,
            Width = 215,
            Height = 34,
            Text = @".\SQLEXPRESS",
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Segoe UI", 10F)
        };

        var lblDatabase = new Label
        {
            Text = "Database :",
            AutoSize = true,
            Left = 420,
            Top = 25,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold)
        };

        cboDatabase = new ComboBox
        {
            Left = 515,
            Top = 19,
            Width = 215,
            Height = 34,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Segoe UI", 10F)
        };

        btnConnect = CreateTopButton("🔗  Kết nối", 760, 16, 120);
        btnConnect.Click += async (_, _) => await ConnectAsync();

        btnRefresh = CreateTopButton("⟳  Tải lại", 895, 16, 120);
        btnRefresh.Enabled = false;
        btnRefresh.Click += async (_, _) => await LoadBothTablesAsync();

        lblStatus = new Label
        {
            Text = "Chưa kết nối",
            AutoSize = true,
            Left = 28,
            Top = 66,
            ForeColor = Color.DimGray,
            Font = new Font("Segoe UI", 9F)
        };

        connectionArea.Controls.AddRange(new Control[]
        {
            lblServer, txtServer,
            lblDatabase, cboDatabase,
            btnConnect, btnRefresh,
            lblStatus
        });

        connectionGroup.Controls.Add(connectionArea);

        // ===== DATA TITLE =====
        var dataTitle = new Label
        {
            Text = "Dữ liệu :",
            Dock = DockStyle.Top,
            Height = 58,
            Padding = new Padding(42, 18, 0, 0),
            Font = new Font("Segoe UI", 12F, FontStyle.Bold),
            ForeColor = Color.Black
        };

        // ===== TABS =====
        tabs = new TabControl
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 10F)
        };

        var tabWeightman = new TabPage("Xe trong ngày")
        {
            BackColor = Color.White,
            Padding = new Padding(0)
        };

        var tabWeightsave = new TabPage("Xe đã lưu")
        {
            BackColor = Color.White,
            Padding = new Padding(0)
        };

        gridWeightman = CreateGrid();
        gridWeightsave = CreateGrid();

        tabWeightman.Controls.Add(CreateGridContainer(gridWeightman));
        tabWeightsave.Controls.Add(CreateGridContainer(gridWeightsave));

        tabs.TabPages.Add(tabWeightman);
        tabs.TabPages.Add(tabWeightsave);

        // ===== BOTTOM ACTION AREA =====
        var bottomPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 120,
            BackColor = Color.White
        };

        btnDeleteAll = new Button
        {
            Text = "✖   Xóa dữ liệu",
            Width = 190,
            Height = 58,
            BackColor = Color.FromArgb(235, 35, 42),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            FlatAppearance =
            {
                BorderSize = 0
            },
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            Anchor = AnchorStyles.Right | AnchorStyles.Top,
            Enabled = false
        };
        btnDeleteAll.Click += async (_, _) => await TruncateAllTablesAsync();

        var deleteHint = new Label
        {
            Text = "Xóa dữ liệu sẽ xóa toàn bộ dữ liệu",
            AutoSize = true,
            ForeColor = Color.Black,
            Font = new Font("Segoe UI", 9F, FontStyle.Italic),
            Anchor = AnchorStyles.Right | AnchorStyles.Top
        };

        bottomPanel.Controls.Add(btnDeleteAll);
        bottomPanel.Controls.Add(deleteHint);

        bottomPanel.Resize += (_, _) =>
        {
            btnDeleteAll.Left = bottomPanel.ClientSize.Width - btnDeleteAll.Width - 55;
            btnDeleteAll.Top = 5;

            deleteHint.Left = bottomPanel.ClientSize.Width - deleteHint.Width - 55;
            deleteHint.Top = 70;
        };

        // ===== ROOT LAYOUT =====
        root.Controls.Add(tabs);
        root.Controls.Add(bottomPanel);
        root.Controls.Add(dataTitle);
        root.Controls.Add(connectionGroup);

        Controls.Add(root);

        cboDatabase.SelectedIndexChanged += DatabaseChanged;
    }

    private static Button CreateTopButton(string text, int left, int top, int width)
    {
        return new Button
        {
            Text = text,
            Left = left,
            Top = top,
            Width = width,
            Height = 38,
            BackColor = Color.FromArgb(190, 190, 190),
            ForeColor = Color.Black,
            FlatStyle = FlatStyle.Flat,
            FlatAppearance =
            {
                BorderSize = 0
            },
            Font = new Font("Segoe UI", 10F, FontStyle.Bold)
        };
    }

    private static Panel CreateGridContainer(DataGridView grid)
    {
        var container = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12, 8, 12, 8),
            BackColor = Color.FromArgb(242, 234, 255)
        };

        container.Controls.Add(grid);
        return container;
    }

    private DataGridView CreateGrid()
    {
        var grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.None,
            AutoGenerateColumns = false,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            RowHeadersVisible = false,
            MultiSelect = false,
            SelectionMode = DataGridViewSelectionMode.CellSelect,
            EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2,
            ReadOnly = false,
            AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None,
            RowTemplate = { Height = 46 },
            ColumnHeadersHeight = 42,
            EnableHeadersVisualStyles = false,
            GridColor = Color.White,
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
            ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None,
            ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Color.White,
                ForeColor = Color.Black,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                Alignment = DataGridViewContentAlignment.MiddleCenter,
                Padding = new Padding(0, 2, 0, 2)
            },
            DefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Color.White,
                ForeColor = Color.Black,
                Font = new Font("Segoe UI", 9.5F),
                Alignment = DataGridViewContentAlignment.MiddleCenter,
                SelectionBackColor = Color.FromArgb(235, 225, 255),
                SelectionForeColor = Color.Black
            },
            AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Color.FromArgb(252, 250, 255),
                SelectionBackColor = Color.FromArgb(235, 225, 255),
                SelectionForeColor = Color.Black
            }
        };

        AddTextColumn(grid, "STT", "ticketnum", 95);
        AddTextColumn(grid, "Số xe", "truckno", 140);
        AddTextColumn(grid, "Khách hàng", "custname", 220);
        AddTextColumn(grid, "Hàng hóa", "Prodname", 230);
        AddTextColumn(grid, "TL lần 1", "Firstweight", 145);
        AddTextColumn(grid, "TL lần 2", "Secondweight", 145);
        AddTextColumn(grid, "Ngày vào", "date_in", 145);
        AddTextColumn(grid, "Giờ vào", "time_in", 125);

        var deleteButton = new DataGridViewButtonColumn
        {
            Name = "DeleteAction",
            HeaderText = "Hành động",
            Text = "🗑",
            UseColumnTextForButtonValue = true,
            Width = 100,
            FlatStyle = FlatStyle.Flat,
            SortMode = DataGridViewColumnSortMode.NotSortable,
            DefaultCellStyle = new DataGridViewCellStyle
            {
                Alignment = DataGridViewContentAlignment.MiddleCenter,
                ForeColor = Color.Red,
                SelectionForeColor = Color.Red,
                Font = new Font("Segoe UI Symbol", 15F)
            }
        };

        grid.Columns.Add(deleteButton);

        // Không cho sửa 3 trường dùng làm khóa nhận diện.
        grid.Columns["ticketnum"].ReadOnly = true;
        grid.Columns["truckno"].ReadOnly = true;
        grid.Columns["date_in"].ReadOnly = true;

        grid.CellBeginEdit += Grid_CellBeginEdit;
        grid.CellEndEdit += Grid_CellEndEdit;
        grid.CellContentClick += Grid_CellContentClick;
        grid.CellFormatting += Grid_CellFormatting;
        grid.DataError += Grid_DataError;

        return grid;
    }

    private static void AddTextColumn(
        DataGridView grid,
        string header,
        string propertyName,
        int width)
    {
        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = propertyName,
            HeaderText = header,
            DataPropertyName = propertyName,
            Width = width,
            SortMode = DataGridViewColumnSortMode.NotSortable,
            ReadOnly = !EditableColumns.Contains(propertyName)
        });
    }

    private void Grid_CellBeginEdit(
        object? sender,
        DataGridViewCellCancelEventArgs e)
    {
        if (sender is not DataGridView grid || e.RowIndex < 0 || e.ColumnIndex < 0)
            return;

        var columnName = grid.Columns[e.ColumnIndex].Name;

        if (!EditableColumns.Contains(columnName))
            e.Cancel = true;
    }

    private async void Grid_CellEndEdit(
        object? sender,
        DataGridViewCellEventArgs e)
    {
        if (sender is not DataGridView grid ||
            e.RowIndex < 0 ||
            e.ColumnIndex < 0)
            return;

        var columnName = grid.Columns[e.ColumnIndex].Name;

        if (!EditableColumns.Contains(columnName))
            return;

        try
        {
            await SaveEditedRowAsync(grid, e.RowIndex);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                "Không thể lưu thay đổi:\n\n" + ex.Message,
                "Lỗi lưu dữ liệu",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);

            await LoadGridAgainAsync(grid);
        }
    }

    private async Task SaveEditedRowAsync(DataGridView grid, int rowIndex)
    {
        if (connection == null)
            return;

        if (grid.Rows[rowIndex].DataBoundItem is not DataRowView rowView)
            return;

        var row = rowView.Row;

        if (row.RowState == DataRowState.Detached)
            return;

        var tableName = grid == gridWeightman
            ? "dbo.Weightman"
            : "dbo.Weightsave";

        const string sql = @"
UPDATE {0}
SET
    [custname] = @custname,
    [Prodname] = @Prodname,
    [Firstweight] = @Firstweight,
    [Secondweight] = @Secondweight,
    [time_in] = @time_in
WHERE
    [Ticketnum] = @Ticketnum
    AND [Truckno] = @Truckno
    AND [Date_in] = @Date_in;";

        using var cmd = new SqlCommand(
            string.Format(sql, tableName),
            connection);

        AddDecimalParameter(cmd, "@Ticketnum", row["ticketnum"]);
        AddStringParameter(cmd, "@Truckno", row["truckno"]);
        AddDateParameter(cmd, "@Date_in", row["date_in"]);

        AddStringParameter(cmd, "@custname", row["custname"]);
        AddStringParameter(cmd, "@Prodname", row["Prodname"]);
        AddDecimalParameter(cmd, "@Firstweight", row["Firstweight"]);
        AddDecimalParameter(cmd, "@Secondweight", row["Secondweight"]);
        AddTimeParameter(cmd, "@time_in", row["time_in"]);

        var affected = await cmd.ExecuteNonQueryAsync();

        if (affected != 1)
        {
            throw new InvalidOperationException(
                $"Không xác định được đúng 1 record để cập nhật. SQL đã cập nhật {affected} dòng.");
        }

        row.AcceptChanges();
    }

    private async void Grid_CellContentClick(
        object? sender,
        DataGridViewCellEventArgs e)
    {
        if (sender is not DataGridView grid ||
            e.RowIndex < 0 ||
            e.ColumnIndex < 0)
            return;

        if (!string.Equals(
                grid.Columns[e.ColumnIndex].Name,
                "DeleteAction",
                StringComparison.OrdinalIgnoreCase))
            return;

        await DeleteSingleRowAsync(grid, e.RowIndex);
    }

    private async Task DeleteSingleRowAsync(
        DataGridView grid,
        int rowIndex)
    {
        if (connection == null)
            return;

        if (grid.Rows[rowIndex].DataBoundItem is not DataRowView rowView)
            return;

        var row = rowView.Row;

        var ticket = Convert.ToString(row["ticketnum"]) ?? "";
        var truck = Convert.ToString(row["truckno"]) ?? "";
        var date = row["date_in"] == DBNull.Value
            ? ""
            : Convert.ToDateTime(row["date_in"]).ToString("dd/MM/yyyy");

        var confirm = MessageBox.Show(
            this,
            $"Bạn có chắc muốn xóa record này?\n\n" +
            $"STT: {ticket}\n" +
            $"Số xe: {truck}\n" +
            $"Ngày vào: {date}\n\n" +
            "Dữ liệu sẽ bị xóa trực tiếp khỏi database.",
            "Xác nhận xóa",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);

        if (confirm != DialogResult.Yes)
            return;

        var tableName = grid == gridWeightman
            ? "dbo.Weightman"
            : "dbo.Weightsave";

        const string sql = @"
DELETE FROM {0}
WHERE
    [Ticketnum] = @Ticketnum
    AND [Truckno] = @Truckno
    AND [Date_in] = @Date_in;";

        using var cmd = new SqlCommand(
            string.Format(sql, tableName),
            connection);

        AddDecimalParameter(cmd, "@Ticketnum", row["ticketnum"]);
        AddStringParameter(cmd, "@Truckno", row["truckno"]);
        AddDateParameter(cmd, "@Date_in", row["date_in"]);

        var affected = await cmd.ExecuteNonQueryAsync();

        if (affected != 1)
        {
            throw new InvalidOperationException(
                $"Không xác định được đúng 1 record để xóa. SQL đã xóa {affected} dòng.");
        }

        await LoadGridAgainAsync(grid);
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
                cboDatabase.Items.Add(row["name"]?.ToString());

            var preferred = cboDatabase.Items
                .Cast<object>()
                .FirstOrDefault(x =>
                    string.Equals(
                        x?.ToString(),
                        "CANTIENPHAT",
                        StringComparison.OrdinalIgnoreCase));

            if (preferred != null)
                cboDatabase.SelectedItem = preferred;
            else if (cboDatabase.Items.Count > 0)
                cboDatabase.SelectedIndex = 0;

            btnRefresh.Enabled = true;
            btnDeleteAll.Enabled = true;

            lblStatus.Text = $"Đã kết nối: {txtServer.Text.Trim()}";

            if (cboDatabase.SelectedItem != null)
                await SelectDatabaseAndLoadAsync(
                    cboDatabase.SelectedItem.ToString()!);
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

    private async void DatabaseChanged(
        object? sender,
        EventArgs e)
    {
        if (cboDatabase.SelectedItem is string database)
            await SelectDatabaseAndLoadAsync(database);
    }

    private async Task SelectDatabaseAndLoadAsync(string database)
    {
        if (connection == null)
            return;

        try
        {
            await connection.CloseAsync();

            var builder = new SqlConnectionStringBuilder(
                connection.ConnectionString)
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
                "Không thể chọn database.\n\n" + ex.Message,
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
            var weightman = await LoadTableAsync("dbo.Weightman");
            var weightsave = await LoadTableAsync("dbo.Weightsave");

            gridWeightman.DataSource = weightman;
            gridWeightsave.DataSource = weightsave;

            ConfigureGridDisplay(gridWeightman);
            ConfigureGridDisplay(gridWeightsave);

            lblStatus.Text =
                $"Đã tải dữ liệu: {cboDatabase.SelectedItem}";
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                "Không thể đọc Weightman / Weightsave.\n\n" + ex.Message,
                "Load error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private async Task<DataTable> LoadTableAsync(string tableName)
    {
        if (connection == null)
            throw new InvalidOperationException("Chưa kết nối SQL Server.");

        var dt = new DataTable();

        var sql =
            $"SELECT " +
            $"[Ticketnum], [Truckno], [Custname], [Prodname], " +
            $"[Firstweight], [Secondweight], [Date_in], [time_in] " +
            $"FROM {tableName} " +
            $"ORDER BY [Date_in], [time_in], [Ticketnum]";

        using var cmd = new SqlCommand(sql, connection);
        using var adapter = new SqlDataAdapter(cmd);

        await Task.Run(() => adapter.Fill(dt));

        return dt;
    }

    private void ConfigureGridDisplay(DataGridView grid)
    {
        grid.ClearSelection();

        if (grid.Columns["ticketnum"] != null)
            grid.Columns["ticketnum"].ReadOnly = true;

        if (grid.Columns["truckno"] != null)
            grid.Columns["truckno"].ReadOnly = true;

        if (grid.Columns["date_in"] != null)
            grid.Columns["date_in"].ReadOnly = true;

        if (grid.Columns["custname"] != null)
            grid.Columns["custname"].ReadOnly = false;

        if (grid.Columns["Prodname"] != null)
            grid.Columns["Prodname"].ReadOnly = false;

        if (grid.Columns["Firstweight"] != null)
            grid.Columns["Firstweight"].ReadOnly = false;

        if (grid.Columns["Secondweight"] != null)
            grid.Columns["Secondweight"].ReadOnly = false;

        if (grid.Columns["time_in"] != null)
            grid.Columns["time_in"].ReadOnly = false;
    }

    private async Task LoadGridAgainAsync(DataGridView grid)
    {
        if (connection == null)
            return;

        var tableName = grid == gridWeightman
            ? "dbo.Weightman"
            : "dbo.Weightsave";

        var table = await LoadTableAsync(tableName);
        grid.DataSource = table;
        ConfigureGridDisplay(grid);
    }

    private async Task TruncateAllTablesAsync()
    {
        if (connection == null)
            return;

        var database = cboDatabase.SelectedItem?.ToString() ?? "";

        var firstConfirm = MessageBox.Show(
            this,
            $"Bạn chuẩn bị XÓA TOÀN BỘ dữ liệu trong database '{database}'.\n\n" +
            "Hai bảng sẽ bị TRUNCATE:\n" +
            "• dbo.Weightman\n" +
            "• dbo.Weightsave\n\n" +
            "Thao tác này không thể hoàn tác.\n\n" +
            "Bạn có chắc chắn muốn tiếp tục?",
            "XÓA TOÀN BỘ DỮ LIỆU",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);

        if (firstConfirm != DialogResult.Yes)
            return;

        var secondConfirm = MessageBox.Show(
            this,
            "XÁC NHẬN LẦN CUỐI\n\n" +
            "Bấm YES để TRUNCATE cả Weightman và Weightsave.",
            "XÁC NHẬN XÓA",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Stop,
            MessageBoxDefaultButton.Button2);

        if (secondConfirm != DialogResult.Yes)
            return;

        try
        {
            btnDeleteAll.Enabled = false;

            // TRUNCATE được thực hiện trong cùng transaction.
            await using var transaction =
                (SqlTransaction)await connection.BeginTransactionAsync();

            try
            {
                using var cmd = new SqlCommand(
                    "TRUNCATE TABLE dbo.Weightsave; " +
                    "TRUNCATE TABLE dbo.Weightman;",
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

            await LoadBothTablesAsync();

            MessageBox.Show(
                this,
                "Đã xóa toàn bộ dữ liệu trong Weightman và Weightsave.",
                "Xóa thành công",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                "Không thể TRUNCATE hai bảng.\n\n" +
                "SQL Server có thể đang có FOREIGN KEY hoặc ràng buộc khác ngăn TRUNCATE.\n\n" +
                ex.Message,
                "Lỗi xóa dữ liệu",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            btnDeleteAll.Enabled = connection != null &&
                                   connection.State == ConnectionState.Open;
        }
    }

    private static void AddStringParameter(
        SqlCommand cmd,
        string name,
        object? value)
    {
        var parameter = cmd.Parameters.Add(
            name,
            System.Data.SqlDbType.NVarChar);

        parameter.Value =
            value == null || value == DBNull.Value
                ? DBNull.Value
                : value;
    }

    private static void AddDecimalParameter(
        SqlCommand cmd,
        string name,
        object? value)
    {
        var parameter = cmd.Parameters.Add(
            name,
            System.Data.SqlDbType.Decimal);

        parameter.Precision = 18;
        parameter.Scale = 3;
        parameter.Value =
            value == null || value == DBNull.Value
                ? DBNull.Value
                : Convert.ToDecimal(value);
    }

    private static void AddDateParameter(
        SqlCommand cmd,
        string name,
        object? value)
    {
        var parameter = cmd.Parameters.Add(
            name,
            System.Data.SqlDbType.Date);

        parameter.Value =
            value == null || value == DBNull.Value
                ? DBNull.Value
                : Convert.ToDateTime(value).Date;
    }

    private static void AddTimeParameter(
        SqlCommand cmd,
        string name,
        object? value)
    {
        var parameter = cmd.Parameters.Add(
            name,
            System.Data.SqlDbType.Time);

        parameter.Value =
            value == null || value == DBNull.Value
                ? DBNull.Value
                : value;
    }

    private void Grid_CellFormatting(
        object? sender,
        DataGridViewCellFormattingEventArgs e)
    {
        if (sender is not DataGridView grid ||
            e.RowIndex < 0 ||
            e.ColumnIndex < 0)
            return;

        var column = grid.Columns[e.ColumnIndex].Name;

        if (column.Equals("date_in", StringComparison.OrdinalIgnoreCase) &&
            e.Value is DateTime date)
        {
            e.Value = date.ToString("dd/MM/yyyy");
            e.FormattingApplied = true;
        }

        if ((column.Equals("Firstweight", StringComparison.OrdinalIgnoreCase) ||
             column.Equals("Secondweight", StringComparison.OrdinalIgnoreCase)) &&
            e.Value != null &&
            e.Value != DBNull.Value)
        {
            if (decimal.TryParse(
                    e.Value.ToString(),
                    out var value))
            {
                e.Value = value.ToString("0.###");
                e.FormattingApplied = true;
            }
        }
    }

    private void Grid_DataError(
        object? sender,
        DataGridViewDataErrorEventArgs e)
    {
        e.ThrowException = false;
    }

    protected override async void OnFormClosed(FormClosedEventArgs e)
    {
        if (connection != null)
            await connection.DisposeAsync();

        base.OnFormClosed(e);
    }
}
