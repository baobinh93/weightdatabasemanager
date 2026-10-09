using Microsoft.Data.SqlClient;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Text.Json;

namespace WeightDatabaseManager;

public class MainForm : Form
{
    private ComboBox txtServer = null!;
    private Button btnFindServers = null!;

    private static readonly string ConfigDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WeightDatabaseManager");
    private static readonly string ConfigPath = Path.Combine(ConfigDirectory, "connections.json");
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

        txtServer = new ComboBox
        {
            Left = 165,
            Top = 19,
            Width = 260,
            Height = 34,
            DropDownStyle = ComboBoxStyle.DropDown,
            Text = @".\SQLEXPRESS",
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10F)
        };
        LoadSavedServers();

        
        var lblDatabase = new Label
        {
            Text = "Database: CANTIENPHAT",
            AutoSize = true,
            Left = 450,
            Top = 25,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold)
        };

        btnConnect = CreateTopButton("🔗  Kết nối", 760, 16, 140);
        SetConnectButtonState(false);
        btnConnect.Click += async (_, _) => await ConnectAsync();

        btnRefresh = CreateTopButton("⟳  Tải lại", 920, 16, 140);
        SetRefreshButtonState(false);
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
            lblServer,
            txtServer,
         
            lblDatabase,
            btnConnect,
            btnRefresh,
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

        btnDeleteAll.Click += async (_, _) =>
            await TruncateAllTablesAsync();

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
            btnDeleteAll.Left =
                bottomPanel.ClientSize.Width -
                btnDeleteAll.Width -
                55;

            btnDeleteAll.Top = 18;

            // Căn giữa dòng cảnh báo theo nút Xóa dữ liệu.
            deleteHint.Left =
                btnDeleteAll.Left +
                (btnDeleteAll.Width - deleteHint.Width) / 2;

            deleteHint.Top =
                btnDeleteAll.Bottom + 8;
        };

        // ===== ROOT LAYOUT =====
        root.Controls.Add(tabs);
        root.Controls.Add(bottomPanel);
        root.Controls.Add(dataTitle);
        root.Controls.Add(connectionGroup);

        Controls.Add(root);
    }

    private static Button CreateTopButton(
        string text,
        int left,
        int top,
        int width)
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
            Font = new Font(
                "Segoe UI",
                10F,
                FontStyle.Bold)
        };
    }

    private void SetConnectButtonState(bool connected)
    {
        if (connected)
        {
            // Sau khi kết nối: Kết nối chuyển sang màu nhạt.
            btnConnect.BackColor =
                Color.FromArgb(225, 225, 225);

            btnConnect.ForeColor =
                Color.DimGray;
        }
        else
        {
            // Chưa kết nối: Kết nối nổi bật.
            btnConnect.BackColor =
                Color.FromArgb(80, 80, 80);

            btnConnect.ForeColor =
                Color.White;
        }
    }

    private void SetRefreshButtonState(bool active)
    {
        if (active)
        {
            // Sau khi kết nối: Tải lại nổi bật.
            btnRefresh.BackColor =
                Color.FromArgb(80, 80, 80);

            btnRefresh.ForeColor =
                Color.White;
        }
        else
        {
            // Chưa kết nối: Tải lại nhạt.
            btnRefresh.BackColor =
                Color.FromArgb(225, 225, 225);

            btnRefresh.ForeColor =
                Color.DimGray;
        }
    }

    private static Panel CreateGridContainer(
        DataGridView grid)
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
            SelectionMode =
                DataGridViewSelectionMode.CellSelect,
            EditMode =
                DataGridViewEditMode.EditOnKeystrokeOrF2,
            ReadOnly = false,
            AutoSizeRowsMode =
                DataGridViewAutoSizeRowsMode.None,
            RowTemplate = { Height = 46 },
            ColumnHeadersHeight = 42,
            EnableHeadersVisualStyles = false,
            GridColor = Color.White,
            CellBorderStyle =
                DataGridViewCellBorderStyle.SingleHorizontal,
            ColumnHeadersBorderStyle =
                DataGridViewHeaderBorderStyle.None,

            ColumnHeadersDefaultCellStyle =
                new DataGridViewCellStyle
                {
                    BackColor = Color.White,
                    ForeColor = Color.Black,
                    Font = new Font(
                        "Segoe UI",
                        10F,
                        FontStyle.Bold),
                    Alignment =
                        DataGridViewContentAlignment.MiddleCenter,
                    Padding = new Padding(0, 2, 0, 2)
                },

            DefaultCellStyle =
                new DataGridViewCellStyle
                {
                    BackColor = Color.White,
                    ForeColor = Color.Black,
                    Font = new Font(
                        "Segoe UI",
                        10F),
                    SelectionBackColor =
                        Color.FromArgb(230, 220, 250),
                    SelectionForeColor = Color.Black,
                    Alignment =
                        DataGridViewContentAlignment.MiddleCenter
                }
        };

        grid.CellFormatting += Grid_CellFormatting;
        grid.DataError += Grid_DataError;

        return grid;
    }

    private void ConfigureGridColumns(
        DataGridView grid)
    {
        grid.Columns.Clear();

        foreach (var columnName in DisplayColumns)
        {
            var column = new DataGridViewTextBoxColumn
            {
                Name = columnName,
                DataPropertyName = columnName,
                HeaderText = GetColumnHeader(columnName),
                SortMode =
                    DataGridViewColumnSortMode.NotSortable,
                ReadOnly =
                    !EditableColumns.Contains(columnName),
                AutoSizeMode =
                    DataGridViewAutoSizeColumnMode.Fill
            };

            grid.Columns.Add(column);
        }
    }

    private static string GetColumnHeader(
        string columnName)
    {
        return columnName.ToLowerInvariant() switch
        {
            "ticketnum" => "STT",
            "truckno" => "Số xe",
            "custname" => "Khách hàng",
            "prodname" => "Hàng hóa",
            "firstweight" => "TL lần 1",
            "secondweight" => "TL lần 2",
            "date_in" => "Ngày",
            "time_in" => "Giờ",
            _ => columnName
        };
    }

    private void ConfigureGridDisplay(
        DataGridView grid)
    {
        ConfigureGridColumns(grid);

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

    private async Task SaveCellAsync(
        DataGridView grid,
        int rowIndex,
        int columnIndex)
    {
        if (connection == null)
            return;

        if (rowIndex < 0 ||
            columnIndex < 0)
            return;

        var column =
            grid.Columns[columnIndex];

        if (!EditableColumns.Contains(column.Name))
            return;

        var row = grid.Rows[rowIndex];

        if (row.IsNewRow)
            return;

        var tableName =
            grid == gridWeightman
                ? "dbo.Weightman"
                : "dbo.Weightsave";

        try
        {
            using var cmd = new SqlCommand(
                $"UPDATE {tableName} SET " +
                $"[{column.Name}] = @Value " +
                $"WHERE [Ticketnum] = @Ticketnum " +
                $"AND [Truckno] = @Truckno " +
                $"AND [Date_in] = @Date_in",
                connection);

            var value =
                row.Cells[columnIndex].Value;

            if (value == null ||
                value == DBNull.Value ||
                string.IsNullOrWhiteSpace(
                    value.ToString()))
            {
                cmd.Parameters.AddWithValue(
                    "@Value",
                    DBNull.Value);
            }
            else if (
                column.Name.Equals(
                    "Firstweight",
                    StringComparison.OrdinalIgnoreCase) ||
                column.Name.Equals(
                    "Secondweight",
                    StringComparison.OrdinalIgnoreCase))
            {
                AddDecimalParameter(
                    cmd,
                    "@Value",
                    value);
            }
            else if (
                column.Name.Equals(
                    "time_in",
                    StringComparison.OrdinalIgnoreCase))
            {
                AddTimeParameter(
                    cmd,
                    "@Value",
                    value);
            }
            else
            {
                AddStringParameter(
                    cmd,
                    "@Value",
                    value);
            }

            AddDecimalParameter(
                cmd,
                "@Ticketnum",
                row.Cells["ticketnum"].Value);

            AddStringParameter(
                cmd,
                "@Truckno",
                row.Cells["truckno"].Value);

            AddDateParameter(
                cmd,
                "@Date_in",
                row.Cells["date_in"].Value);

            var affected =
                await cmd.ExecuteNonQueryAsync();

            if (affected != 1)
            {
                throw new InvalidOperationException(
                    $"Không xác định được đúng 1 record để cập nhật. SQL đã cập nhật {affected} dòng.");
            }

            lblStatus.Text =
                "Đã lưu thay đổi.";
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                "Không thể lưu thay đổi.\n\n" +
                ex.Message,
                "Save error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);

            await LoadGridAgainAsync(grid);
        }
    }

    private async Task DeleteRowAsync(
        DataGridView grid,
        int rowIndex)
    {
        if (connection == null)
            return;

        if (rowIndex < 0)
            return;

        var row = grid.Rows[rowIndex];

        if (row.IsNewRow)
            return;

        var tableName =
            grid == gridWeightman
                ? "dbo.Weightman"
                : "dbo.Weightsave";

        var confirm =
            MessageBox.Show(
                this,
                "Bạn có chắc muốn xóa record này?",
                "Xóa record",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);

        if (confirm != DialogResult.Yes)
            return;

        try
        {
            using var cmd = new SqlCommand(
                $"DELETE FROM {tableName} " +
                $"WHERE [Ticketnum] = @Ticketnum " +
                $"AND [Truckno] = @Truckno " +
                $"AND [Date_in] = @Date_in",
                connection);

            AddDecimalParameter(
                cmd,
                "@Ticketnum",
                row.Cells["ticketnum"].Value);

            AddStringParameter(
                cmd,
                "@Truckno",
                row.Cells["truckno"].Value);

            AddDateParameter(
                cmd,
                "@Date_in",
                row.Cells["date_in"].Value);

            var affected =
                await cmd.ExecuteNonQueryAsync();

            if (affected != 1)
            {
                throw new InvalidOperationException(
                    $"Không xác định được đúng 1 record để xóa. SQL đã xóa {affected} dòng.");
            }

            await LoadGridAgainAsync(grid);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                "Không thể xóa record.\n\n" +
                ex.Message,
                "Delete error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    // Nạp các server đã kết nối thành công ở những lần chạy trước.
    private void LoadSavedServers()
    {
        try
        {
            if (!File.Exists(ConfigPath))
                return;

            var saved = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(ConfigPath));
            if (saved == null)
                return;

            foreach (var server in saved.Where(x => !string.IsNullOrWhiteSpace(x)))
            {
                if (!txtServer.Items.Contains(server))
                    txtServer.Items.Add(server);
            }
        }
        catch
        {
            // Nếu file cấu hình lỗi, vẫn cho phép nhập server thủ công.
        }
    }

    // Chỉ lưu tên server sau khi kết nối và xác nhận database thành công.
    private void SaveSuccessfulServer(string server)
    {
        if (string.IsNullOrWhiteSpace(server))
            return;

        try
        {
            Directory.CreateDirectory(ConfigDirectory);
            var saved = new List<string>();

            if (File.Exists(ConfigPath))
            {
                saved = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(ConfigPath))
                    ?? new List<string>();
            }

            saved.RemoveAll(x => string.Equals(x, server, StringComparison.OrdinalIgnoreCase));
            saved.Insert(0, server);
            File.WriteAllText(ConfigPath, JsonSerializer.Serialize(saved, new JsonSerializerOptions
            {
                WriteIndented = true
            }));

            if (!txtServer.Items.Contains(server))
                txtServer.Items.Insert(0, server);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this,
                "Kết nối thành công nhưng không lưu được server vào danh sách.\n\n" + ex.Message,
                "Lưu cấu hình", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    // Gợi ý các tên server thường gặp. Đây không phải quét toàn mạng;
    // người dùng vẫn có thể nhập tên server bất kỳ bằng tay.
   
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
                InitialCatalog = "CANTIENPHAT",
                IntegratedSecurity = true,
                TrustServerCertificate = true,
                ConnectTimeout = 5
            };

            connection =
                new SqlConnection(
                    builder.ConnectionString);

            await connection.OpenAsync();

            // Kiểm tra database CANTIENPHAT có tồn tại
            // và đang ONLINE.
            using (var cmd = new SqlCommand(
                "SELECT COUNT(1) FROM sys.databases " +
                "WHERE name = 'CANTIENPHAT' " +
                "AND state_desc = 'ONLINE'",
                connection))
            {
                var exists =
                    Convert.ToInt32(
                        await cmd.ExecuteScalarAsync()) > 0;

                if (!exists)
                {
                    throw new InvalidOperationException(
                        "Không tìm thấy database 'CANTIENPHAT' hoặc database không ở trạng thái ONLINE.");
                }
            }

            btnRefresh.Enabled = true;
            btnDeleteAll.Enabled = true;

            SetConnectButtonState(true);
            SetRefreshButtonState(true);

            SaveSuccessfulServer(txtServer.Text.Trim());
            lblStatus.Text =
                $"Đã kết nối: {txtServer.Text.Trim()} - CANTIENPHAT";

            await LoadBothTablesAsync();
        }
        catch (Exception ex)
        {
            SetConnectButtonState(false);
            SetRefreshButtonState(false);

            btnRefresh.Enabled = false;
            btnDeleteAll.Enabled = false;

            lblStatus.Text =
                "Kết nối thất bại";

            MessageBox.Show(
                this,
                "Không thể kết nối SQL Server.\n\n" +
                ex.Message,
                "Connection error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            btnConnect.Enabled = true;
        }
    }

    private async Task LoadBothTablesAsync()
    {
        if (connection == null)
            return;

        try
        {
            var weightman =
                await LoadTableAsync(
                    "dbo.Weightman");

            var weightsave =
                await LoadTableAsync(
                    "dbo.Weightsave");

            gridWeightman.DataSource =
                weightman;

            gridWeightsave.DataSource =
                weightsave;

            ConfigureGridDisplay(
                gridWeightman);

            ConfigureGridDisplay(
                gridWeightsave);

            lblStatus.Text =
                "Đã tải dữ liệu: CANTIENPHAT";
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                "Không thể đọc Weightman / Weightsave.\n\n" +
                ex.Message,
                "Load error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private async Task<DataTable> LoadTableAsync(
        string tableName)
    {
        if (connection == null)
        {
            throw new InvalidOperationException(
                "Chưa kết nối SQL Server.");
        }

        var dt = new DataTable();

        var sql =
            $"SELECT " +
            $"[Ticketnum], [Truckno], [Custname], [Prodname], " +
            $"[Firstweight], [Secondweight], [Date_in], [time_in] " +
            $"FROM {tableName} " +
            $"ORDER BY [Date_in], [time_in], [Ticketnum]";

        using var cmd =
            new SqlCommand(sql, connection);

        using var adapter =
            new SqlDataAdapter(cmd);

        await Task.Run(
            () => adapter.Fill(dt));

        return dt;
    }

    private async Task LoadGridAgainAsync(
        DataGridView grid)
    {
        if (connection == null)
            return;

        var tableName =
            grid == gridWeightman
                ? "dbo.Weightman"
                : "dbo.Weightsave";

        var table =
            await LoadTableAsync(tableName);

        grid.DataSource = table;

        ConfigureGridDisplay(grid);
    }

    private async Task TruncateAllTablesAsync()
    {
        if (connection == null)
            return;

        const string database =
            "CANTIENPHAT";

        var firstConfirm =
            MessageBox.Show(
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

        var secondConfirm =
            MessageBox.Show(
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

            // TRUNCATE được thực hiện
            // trong cùng transaction.
            await using var transaction =
                (SqlTransaction)
                await connection.BeginTransactionAsync();

            try
            {
                using var cmd =
                    new SqlCommand(
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
            btnDeleteAll.Enabled =
                connection != null &&
                connection.State ==
                ConnectionState.Open;
        }
    }

    private static void AddStringParameter(
        SqlCommand cmd,
        string name,
        object? value)
    {
        var parameter =
            cmd.Parameters.Add(
                name,
                System.Data.SqlDbType.NVarChar);

        parameter.Value =
            value == null ||
            value == DBNull.Value
                ? DBNull.Value
                : value;
    }

    private static void AddDecimalParameter(
        SqlCommand cmd,
        string name,
        object? value)
    {
        var parameter =
            cmd.Parameters.Add(
                name,
                System.Data.SqlDbType.Decimal);

        parameter.Precision = 18;
        parameter.Scale = 3;

        parameter.Value =
            value == null ||
            value == DBNull.Value
                ? DBNull.Value
                : Convert.ToDecimal(value);
    }

    private static void AddDateParameter(
        SqlCommand cmd,
        string name,
        object? value)
    {
        var parameter =
            cmd.Parameters.Add(
                name,
                System.Data.SqlDbType.Date);

        parameter.Value =
            value == null ||
            value == DBNull.Value
                ? DBNull.Value
                : Convert.ToDateTime(value).Date;
    }

    private static void AddTimeParameter(
        SqlCommand cmd,
        string name,
        object? value)
    {
        var parameter =
            cmd.Parameters.Add(
                name,
                System.Data.SqlDbType.Time);

        parameter.Value =
            value == null ||
            value == DBNull.Value
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

        var column =
            grid.Columns[e.ColumnIndex].Name;

        if (column.Equals(
                "date_in",
                StringComparison.OrdinalIgnoreCase) &&
            e.Value is DateTime date)
        {
            e.Value =
                date.ToString("dd/MM/yyyy");

            e.FormattingApplied = true;
        }

        if (
            (column.Equals(
                "Firstweight",
                StringComparison.OrdinalIgnoreCase) ||
             column.Equals(
                "Secondweight",
                StringComparison.OrdinalIgnoreCase)) &&
            e.Value != null &&
            e.Value != DBNull.Value)
        {
            if (decimal.TryParse(
                    e.Value.ToString(),
                    out var value))
            {
                e.Value =
                    value.ToString("0.###");

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

    protected override async void OnFormClosed(
        FormClosedEventArgs e)
    {
        if (connection != null)
            await connection.DisposeAsync();

        base.OnFormClosed(e);
    }
}
