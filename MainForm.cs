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
    private Label lblStatus = null!;
    private TabControl tabs = null!;
    private DataGridView gridWeightman = null!;
    private DataGridView gridWeightsave = null!;
    private Button btnSaveWeightman = null!;
    private Button btnSaveWeightsave = null!;

    private SqlConnection? connection;
    private DataTable? weightmanTable;
    private DataTable? weightsaveTable;

    public MainForm()
    {
        Text = "Weight Database Manager";
        Width = 1250;
        Height = 760;
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1000, 600);

        BuildUi();
    }

    private void BuildUi()
    {
        var connectionGroup = new GroupBox
        {
            Text = "Connect to Server",
            Dock = DockStyle.Top,
            Height = 120,
            Padding = new Padding(12)
        };

        var lblServer = new Label { Text = "Server name:", AutoSize = true, Left = 18, Top = 30 };
        txtServer = new TextBox
        {
            Left = 115, Top = 26, Width = 220,
            Text = @".\SQLEXPRESS"
        };

        var lblDatabase = new Label { Text = "Database:", AutoSize = true, Left = 365, Top = 30 };
        cboDatabase = new ComboBox
        {
            Left = 430, Top = 26, Width = 220,
            DropDownStyle = ComboBoxStyle.DropDownList
        };

        btnConnect = new Button { Text = "Connect", Left = 675, Top = 24, Width = 110, Height = 30 };
        btnConnect.Click += async (_, _) => await ConnectAsync();

        btnRefresh = new Button { Text = "Refresh", Left = 795, Top = 24, Width = 100, Height = 30, Enabled = false };
        btnRefresh.Click += async (_, _) => await LoadBothTablesAsync();

        lblStatus = new Label
        {
            Text = "Not connected",
            AutoSize = true,
            Left = 18,
            Top = 70
        };

        connectionGroup.Controls.AddRange([lblServer, txtServer, lblDatabase, cboDatabase, btnConnect, btnRefresh, lblStatus]);

        tabs = new TabControl { Dock = DockStyle.Fill };
        var tabWeightman = new TabPage("dbo.Weightman");
        var tabWeightsave = new TabPage("dbo.Weightsave");

        gridWeightman = CreateGrid();
        gridWeightsave = CreateGrid();

        btnSaveWeightman = new Button { Text = "Save changes", Dock = DockStyle.Bottom, Height = 38, Enabled = false };
        btnSaveWeightsave = new Button { Text = "Save changes", Dock = DockStyle.Bottom, Height = 38, Enabled = false };

        btnSaveWeightman.Click += async (_, _) => await SaveTableAsync("dbo.Weightman", gridWeightman);
        btnSaveWeightsave.Click += async (_, _) => await SaveTableAsync("dbo.Weightsave", gridWeightsave);

        tabWeightman.Controls.Add(gridWeightman);
        tabWeightman.Controls.Add(btnSaveWeightman);

        tabWeightsave.Controls.Add(gridWeightsave);
        tabWeightsave.Controls.Add(btnSaveWeightsave);

        tabs.TabPages.Add(tabWeightman);
        tabs.TabPages.Add(tabWeightsave);

        var bottomPanel = new Panel { Dock = DockStyle.Bottom, Height = 58, Padding = new Padding(10) };
        btnReset = new Button
        {
            Text = "RESET",
            Width = 130,
            Height = 38,
            Left = 10,
            Top = 10,
            BackColor = Color.Firebrick,
            ForeColor = Color.White,
            Font = new Font(Font, FontStyle.Bold),
            Enabled = false
        };
        btnReset.Click += async (_, _) => await ResetTablesAsync();

        var warning = new Label
        {
            Text = "RESET sẽ xóa toàn bộ dữ liệu của dbo.Weightman và dbo.Weightsave bằng TRUNCATE TABLE.",
            AutoSize = true,
            Left = 155,
            Top = 20,
            ForeColor = Color.Firebrick
        };

        bottomPanel.Controls.Add(btnReset);
        bottomPanel.Controls.Add(warning);

        Controls.Add(tabs);
        Controls.Add(bottomPanel);
        Controls.Add(connectionGroup);
    }

    private static DataGridView CreateGrid()
    {
        return new DataGridView
        {
            Dock = DockStyle.Fill,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells,
            AllowUserToAddRows = true,
            AllowUserToDeleteRows = true,
            EditMode = DataGridViewEditMode.EditOnDoubleClick,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            RowHeadersVisible = false
        };
    }

    private async Task ConnectAsync()
    {
        try
        {
            btnConnect.Enabled = false;
            lblStatus.Text = "Connecting...";

            if (connection != null)
            {
                await connection.DisposeAsync();
                connection = null;
            }

            // Windows Authentication only. No username/password fields.
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
                "SELECT name FROM sys.databases WHERE state_desc = 'ONLINE' ORDER BY name", connection))
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                dbs.Load(reader);
            }

            cboDatabase.Items.Clear();
            foreach (DataRow row in dbs.Rows)
                cboDatabase.Items.Add(row["name"].ToString());

            var preferred = cboDatabase.Items.Cast<object>()
                .FirstOrDefault(x => string.Equals(x?.ToString(), "CANTHUONG", StringComparison.OrdinalIgnoreCase));

            if (preferred != null)
                cboDatabase.SelectedItem = preferred;
            else if (cboDatabase.Items.Count > 0)
                cboDatabase.SelectedIndex = 0;

            btnRefresh.Enabled = true;
            btnReset.Enabled = true;
            lblStatus.Text = $"Connected: {txtServer.Text.Trim()}";

            if (cboDatabase.SelectedItem != null)
                await SelectDatabaseAndLoadAsync(cboDatabase.SelectedItem.ToString()!);

            cboDatabase.SelectedIndexChanged -= DatabaseChanged;
            cboDatabase.SelectedIndexChanged += DatabaseChanged;
        }
        catch (Exception ex)
        {
            lblStatus.Text = "Connection failed";
            MessageBox.Show(this,
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
        if (connection == null) return;

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
            MessageBox.Show(this, ex.Message, "Database error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task LoadBothTablesAsync()
    {
        if (connection == null) return;

        try
        {
            weightmanTable = await LoadTableAsync("dbo.Weightman");
            weightsaveTable = await LoadTableAsync("dbo.Weightsave");

            gridWeightman.DataSource = weightmanTable;
            gridWeightsave.DataSource = weightsaveTable;

            btnSaveWeightman.Enabled = HasPrimaryKey(weightmanTable);
            btnSaveWeightsave.Enabled = HasPrimaryKey(weightsaveTable);

            lblStatus.Text = $"Loaded: {cboDatabase.SelectedItem}";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this,
                "Không thể đọc Weightman/Weightsave.\n\n" + ex.Message,
                "Load error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private async Task<DataTable> LoadTableAsync(string tableName)
    {
        var dt = new DataTable();
        using var cmd = new SqlCommand($"SELECT * FROM {tableName}", connection);
        using var adapter = new SqlDataAdapter(cmd);
        await Task.Run(() => adapter.Fill(dt));
        return dt;
    }

    private static bool HasPrimaryKey(DataTable? dt)
        => dt != null && dt.PrimaryKey.Length > 0;

    private async Task SaveTableAsync(string tableName, DataGridView grid)
    {
        if (connection == null) return;

        try
        {
            var dt = grid.DataSource as DataTable;
            if (dt == null || dt.PrimaryKey.Length == 0)
            {
                MessageBox.Show(this,
                    "Bảng này không có Primary Key nên app chưa cho phép cập nhật an toàn.",
                    "Cannot save",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            grid.EndEdit();

            using var adapter = new SqlDataAdapter($"SELECT * FROM {tableName}", connection);
            using var builder = new SqlCommandBuilder(adapter);

            adapter.Update(dt);
            await LoadBothTablesAsync();

            MessageBox.Show(this, "Đã lưu thay đổi.", "Saved",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this,
                "Không thể lưu thay đổi.\n\n" + ex.Message,
                "Save error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private async Task ResetTablesAsync()
    {
        if (connection == null) return;

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

            // Explicitly requested: reset using TRUNCATE.
            // If SQL Server reports a FK constraint, the operation will fail
            // rather than silently switching to DELETE.
            using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();

            try
            {
                // Try the save table first; if it references Weightman,
                // this is the natural child -> parent order.
                using (var cmd = new SqlCommand(
                    "TRUNCATE TABLE dbo.Weightsave; TRUNCATE TABLE dbo.Weightman;",
                    connection, transaction))
                {
                    await cmd.ExecuteNonQueryAsync();
                }

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }

            await LoadBothTablesAsync();

            MessageBox.Show(this,
                "Đã RESET thành công 2 bảng.",
                "RESET complete",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this,
                "RESET không thực hiện được.\n\n" +
                "SQL Server có thể đang có FOREIGN KEY hoặc ràng buộc khiến TRUNCATE không được phép.\n\n" +
                ex.Message,
                "RESET error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            btnReset.Enabled = true;
        }
    }

    protected override async void OnFormClosed(FormClosedEventArgs e)
    {
        if (connection != null)
            await connection.DisposeAsync();

        base.OnFormClosed(e);
    }
}
