using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace WinTool
{
    // =====================================================================
    //  BAT 命令库
    // =====================================================================
    internal class BatchPage : Page
    {
        public override string Title { get { return "BAT 命令库"; } }
        public override string Subtitle
        {
            get { return "把多步操作打包成一条脚本：既能直接运行，也能另存为 .bat 反复使用。"; }
        }

        private ListView _list;
        private TextBox _code;
        private TextBox _custom;
        private ComboBox _filter;
        private Label _desc;
        private TextBox _result;
        private List<BatItem> _items = new List<BatItem>();
        private bool _running;

        protected override void Build()
        {
            int W = 928, x = 24, y = 20;

            // ---------- 命令库 ----------
            Panel lib = Ui.Put(Ui.Card(x, y, W, 452), Host);
            lib.Controls.Add(Ui.Text("常用命令脚本", 20, 16, 12f, T.Text, true));
            lib.Controls.Add(Ui.Sub("单击查看脚本内容，双击直接运行", 130, 20));

            _filter = Ui.Combo(680, 14, 130);
            _filter.Items.Add("全部分类");
            _items = BatchLib.All();
            string[] cats = BatchLib.Categories(_items);
            for (int i = 0; i < cats.Length; i++) _filter.Items.Add(cats[i]);
            _filter.SelectedIndex = 0;
            _filter.SelectedIndexChanged += delegate { Fill(); };
            lib.Controls.Add(_filter);

            FlatBtn openDir = Ui.Btn("脚本目录", 818, 12, 90, 30, T.Text, delegate { Bat.OpenScriptFolder(); }, true);
            lib.Controls.Add(openDir);

            _list = new ListView();
            _list.Location = new Point(20, 52);
            _list.Size = new Size(340, 322);
            _list.View = View.Details;
            _list.FullRowSelect = true;
            _list.MultiSelect = false;
            _list.Font = T.F(9f);
            _list.Columns.Add("脚本名称", 210);
            _list.Columns.Add("分类", 70);
            _list.Columns.Add("权限", 50);
            _list.SelectedIndexChanged += delegate { ShowCode(); };
            _list.DoubleClick += delegate { RunWindow(false); };
            lib.Controls.Add(_list);

            _desc = Ui.Sub("", 376, 54);
            _desc.AutoSize = false;
            _desc.Size = new Size(532, 40);
            lib.Controls.Add(_desc);

            _code = Ui.Multi(376, 96, 532, 278);
            _code.Font = T.M(9f);
            lib.Controls.Add(_code);

            lib.Controls.Add(MkBtn("直接运行", 20, 388, 100, 32, delegate { RunWindow(false); }, false));
            lib.Controls.Add(MkBtn("以管理员运行", 130, 388, 130, 32, delegate { RunWindow(true); }, true));
            lib.Controls.Add(MkBtn("后台运行并取回结果", 270, 388, 160, 32, delegate { RunCapture(); }, true));
            lib.Controls.Add(MkBtn("另存为 .bat", 440, 388, 120, 32, delegate { SaveAs(); }, true));
            lib.Controls.Add(MkBtn("复制脚本", 570, 388, 100, 32, delegate { Sys.Copy(_code.Text); Form.SetStatus("脚本内容已复制到剪贴板"); }, true));
            lib.Controls.Add(MkBtn("打开脚本目录", 680, 388, 130, 32, delegate { Bat.OpenScriptFolder(); }, true));

            y += 452 + 12;

            // ---------- 自定义脚本 ----------
            Panel cus = Ui.Put(Ui.Card(x, y, W, 300), Host);
            Ui.DrawIcon(cus, 18, 22, 22, "code", T.Accent);
            cus.Controls.Add(Ui.Text("自定义 BAT 脚本", 52, 22, 11.5f, T.Text, true));
            cus.Controls.Add(Ui.Sub("在这里写任意批处理命令，可直接运行或保存到脚本目录", 52, 44));

            _custom = new TextBox();
            _custom.Location = new Point(20, 76);
            _custom.Size = new Size(888, 150);
            _custom.Multiline = true;
            _custom.ScrollBars = ScrollBars.Both;
            _custom.WordWrap = false;
            _custom.Font = T.M(9f);
            _custom.BorderStyle = BorderStyle.FixedSingle;
            _custom.BackColor = Color.FromArgb(252, 253, 255);
            _custom.Text =
                "@echo off\r\n" +
                "chcp 65001 >nul\r\n" +
                "rem 在这里写你的命令，例如下面这几行：\r\n" +
                "echo 当前时间：%date% %time%\r\n" +
                "ipconfig | findstr /c:\"IPv4\"\r\n" +
                "pause\r\n";
            cus.Controls.Add(_custom);

            cus.Controls.Add(MkBtn("运行（新窗口）", 20, 240, 140, 32, delegate { RunCustom(false); }, false));
            cus.Controls.Add(MkBtn("管理员运行", 170, 240, 110, 32, delegate { RunCustom(true); }, true));
            cus.Controls.Add(MkBtn("后台运行并取回结果", 290, 240, 160, 32, delegate { RunCustomCapture(); }, true));
            cus.Controls.Add(MkBtn("保存脚本", 460, 240, 100, 32, delegate { SaveCustom(); }, true));

            y += 300 + 12;

            // ---------- 结果输出 ----------
            Panel res = Ui.Put(Ui.Card(x, y, W, 240), Host);
            res.Controls.Add(Ui.Text("执行结果", 20, 16, 12f, T.Text, true));
            _result = Ui.Multi(20, 48, 888, 176);
            res.Controls.Add(_result);

            y += 240 + 12;

            Host.Controls.Add(Ui.Note(x, y, W,
                "重要提示：\r\n" +
                "1. “直接运行”会打开一个命令行窗口，你能实时看到每一步输出，窗口不会自动关闭（方便核对结果）。\r\n" +
                "2. 标记为“管理员”的脚本会弹出 UAC 提权窗口；若点“否”，脚本不会执行。\r\n" +
                "3. 脚本中的删除、重置操作都是真实生效的，运行前请先看清脚本内容。\r\n" +
                "4. 另存的脚本保存在程序目录的 scripts 文件夹，绿色版复制整个文件夹即可带走。\r\n" +
                "5. 后台运行方式会等脚本结束并取回文字结果，适合查看信息类脚本（最长等待 2 分钟）。",
                T.Warn, 150));

            Host.AutoScrollMinSize = new Size(W + 48, y + 170);
            Fill();
        }

        private FlatBtn MkBtn(string text, int x, int y, int w, int h, EventHandler handler, bool outline)
        {
            FlatBtn b = Ui.Btn(text, x, y, w, h, T.Accent, null, outline);
            b.Click += handler;
            return b;
        }

        private void Fill()
        {
            string cat = _filter.SelectedIndex <= 0 ? null : _filter.SelectedItem.ToString();
            _list.BeginUpdate();
            _list.Items.Clear();
            for (int i = 0; i < _items.Count; i++)
            {
                if (cat != null && _items[i].Cat != cat) continue;
                ListViewItem li = new ListViewItem(_items[i].Name);
                li.SubItems.Add(_items[i].Cat);
                li.SubItems.Add(_items[i].Admin ? "管理员" : "普通");
                if (_items[i].Admin) li.ForeColor = T.Warn;
                li.Tag = _items[i];
                _list.Items.Add(li);
            }
            _list.EndUpdate();
            if (_list.Items.Count > 0) _list.Items[0].Selected = true;
        }

        private BatItem Selected()
        {
            if (_list.SelectedItems.Count == 0) return null;
            return _list.SelectedItems[0].Tag as BatItem;
        }

        private void ShowCode()
        {
            BatItem it = Selected();
            if (it == null) return;
            _code.Text = it.Code;
            _code.SelectionStart = 0;
            _desc.Text = "【" + it.Cat + "】" + it.Desc + (it.Admin ? "   · 需要管理员权限" : "") +
                (string.IsNullOrEmpty(it.Warn) ? "" : "\r\n⚠ " + it.Warn);
        }

        private void RunWindow(bool forceAdmin)
        {
            BatItem it = Selected();
            if (it == null) { Info("请先选择一个脚本。"); return; }
            if ((forceAdmin || it.Admin) && !Sys.Admin)
            {
                DialogResult dr = MessageBox.Show(Form,
                    "该脚本需要管理员权限。\n\n系统会弹出 UAC 提权窗口，请选择“是”。\n\n是否继续？",
                    "需要管理员权限", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (dr != DialogResult.Yes) return;
            }
            if (!string.IsNullOrEmpty(it.Warn))
            {
                DialogResult dr = MessageBox.Show(Form, "⚠ 风险提示\n\n" + it.Warn + "\n\n确定继续运行该脚本吗？",
                    "风险提示", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (dr != DialogResult.Yes) return;
            }
            Bat.RunInWindow(it.Code, forceAdmin || it.Admin, it.Name);
            Form.SetStatus("已启动脚本：" + it.Name);
        }

        private void RunCapture()
        {
            BatItem it = Selected();
            if (it == null) { Info("请先选择一个脚本。"); return; }
            StartCapture(it.Code, it.Name);
        }

        private void StartCapture(string code, string label)
        {
            if (_running) { Info("上一个脚本还在执行中，请稍候。"); return; }
            _running = true;
            Form.SetStatus("正在后台执行脚本：" + label);
            System.Threading.ThreadPool.QueueUserWorkItem(delegate(object st)
            {
                Res r = new Res();
                try { r = Bat.RunCapture(code, 120000); }
                catch (Exception ex) { r.Code = -1; r.Err = ex.Message; }
                try
                {
                    Form.BeginInvoke((MethodInvoker)delegate
                    {
                        _running = false;
                        StringBuilder sb = new StringBuilder();
                        sb.AppendLine("脚本：" + label);
                        sb.AppendLine("时间：" + DateTime.Now.ToString("HH:mm:ss") + "    返回码：" + r.Code + (r.TimedOut ? "（执行超时，已强制结束）" : ""));
                        if (!string.IsNullOrEmpty(r.Out)) sb.AppendLine().AppendLine("---- 标准输出 ----").AppendLine(r.Out);
                        if (!string.IsNullOrEmpty(r.Err)) sb.AppendLine().AppendLine("---- 错误输出 ----").AppendLine(r.Err);
                        if (string.IsNullOrEmpty(r.Out) && string.IsNullOrEmpty(r.Err)) sb.AppendLine().AppendLine("（脚本没有产生任何输出。若脚本含 pause，请改用“直接运行”。）");
                        _result.Text = sb.ToString();
                        _result.SelectionStart = 0;
                        Form.SetStatus("脚本执行完成，返回码 " + r.Code);
                    });
                }
                catch { }
            });
        }

        private void SaveAs()
        {
            BatItem it = Selected();
            if (it == null) return;
            string p = Bat.Save(it.Name, it.Code);
            if (p != null) Info("脚本已保存到：\n" + p + "\n\n双击该 .bat 文件即可运行。");
        }

        private void RunCustom(bool asAdmin)
        {
            if (_custom.Text.Trim().Length == 0) { Info("请先输入脚本内容。"); return; }
            if (asAdmin && !Sys.Admin)
            {
                DialogResult dr = MessageBox.Show(Form, "将以管理员身份运行自定义脚本，会弹出 UAC 提权窗口。\n\n继续吗？",
                    "确认", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (dr != DialogResult.Yes) return;
            }
            Bat.RunInWindow(_custom.Text, asAdmin, "自定义 BAT");
            Form.SetStatus("已启动自定义脚本");
        }

        private void RunCustomCapture()
        {
            if (_custom.Text.Trim().Length == 0) { Info("请先输入脚本内容。"); return; }
            StartCapture(_custom.Text, "自定义脚本");
        }

        private void SaveCustom()
        {
            if (_custom.Text.Trim().Length == 0) { Info("请先输入脚本内容。"); return; }
            string name = "自定义脚本-" + DateTime.Now.ToString("MMdd-HHmm");
            string p = Bat.Save(name, _custom.Text);
            if (p != null) Info("已保存到：\n" + p);
        }

        private void Info(string text)
        {
            MessageBox.Show(Form, text, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
