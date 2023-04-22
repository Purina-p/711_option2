namespace Client
{
    partial class ClientForm
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            label1 = new Label();
            button1 = new Button();
            label2 = new Label();
            listBox1 = new ListBox();
            button2 = new Button();
            textBox1 = new TextBox();
            pictureBox1 = new PictureBox();
            button3 = new Button();
            ((System.ComponentModel.ISupportInitialize)pictureBox1).BeginInit();
            SuspendLayout();
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(41, 9);
            label1.Name = "label1";
            label1.Size = new Size(38, 15);
            label1.TabIndex = 0;
            label1.Text = "label1";
            // 
            // button1
            // 
            button1.Location = new Point(21, 54);
            button1.Margin = new Padding(2);
            button1.Name = "button1";
            button1.Size = new Size(115, 21);
            button1.TabIndex = 1;
            button1.Text = "Check List";
            button1.UseVisualStyleBackColor = true;
            button1.Click += button1_Click;
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new Point(55, 73);
            label2.Name = "label2";
            label2.Size = new Size(38, 15);
            label2.TabIndex = 3;
            label2.Text = "label2";
            // 
            // listBox1
            // 
            listBox1.FormattingEnabled = true;
            listBox1.ItemHeight = 15;
            listBox1.Location = new Point(21, 90);
            listBox1.Margin = new Padding(2);
            listBox1.Name = "listBox1";
            listBox1.Size = new Size(132, 394);
            listBox1.TabIndex = 4;
            listBox1.SelectedIndexChanged += listBox1_SelectedIndexChanged_1;
            // 
            // button2
            // 
            button2.Location = new Point(185, 54);
            button2.Name = "button2";
            button2.Size = new Size(115, 23);
            button2.TabIndex = 5;
            button2.Text = "DownLoadText";
            button2.UseVisualStyleBackColor = true;
            button2.Click += button2_Click;
            // 
            // textBox1
            // 
            textBox1.Location = new Point(185, 90);
            textBox1.Multiline = true;
            textBox1.Name = "textBox1";
            textBox1.ScrollBars = ScrollBars.Vertical;
            textBox1.Size = new Size(412, 400);
            textBox1.TabIndex = 6;
            // 
            // pictureBox1
            // 
            pictureBox1.Location = new Point(632, 90);
            pictureBox1.Name = "pictureBox1";
            pictureBox1.Size = new Size(274, 394);
            pictureBox1.TabIndex = 7;
            pictureBox1.TabStop = false;
            // 
            // button3
            // 
            button3.Location = new Point(632, 55);
            button3.Name = "button3";
            button3.Size = new Size(139, 23);
            button3.TabIndex = 8;
            button3.Text = "DownLoadImage";
            button3.UseVisualStyleBackColor = true;
            button3.Click += button3_Click;
            // 
            // ClientForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1008, 502);
            Controls.Add(button3);
            Controls.Add(pictureBox1);
            Controls.Add(textBox1);
            Controls.Add(button2);
            Controls.Add(listBox1);
            Controls.Add(label2);
            Controls.Add(button1);
            Controls.Add(label1);
            Name = "ClientForm";
            Text = "ClientForm";
            Load += ClientForm_Load;
            ((System.ComponentModel.ISupportInitialize)pictureBox1).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }


        #endregion

        private Label label1;
        private Button button1;
        private Label label2;
        private ListBox listBox1;
        private Button button2;
        private TextBox textBox1;
        private PictureBox pictureBox1;
        private Button button3;
    }
}