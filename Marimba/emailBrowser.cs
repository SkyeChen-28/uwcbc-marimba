﻿using Marimba.Utility;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Web;
using System.Xml;
using ImapX.Constants;
using ImapX.Enums;

namespace Marimba
{
    partial class emailBrowser : Form
    {
        public Enumerations.EmailPurpose use;
        int messageIndex;
        IList<int> fromIndexList = new List<int>();
        bool fromMember = false;

        public emailBrowser(Enumerations.EmailPurpose use, int messageIndex = -1, IList<int> fromIndexList = null)
        {
            //set up this form so it's all ready to go for whatever purpose we need
            this.use = use;
            InitializeComponent();
            if (use == Enumerations.EmailPurpose.Receive)
            {
                rtbWrite.Dispose();
                btnSend.TabIndex--;
                tlpMain.SetColumnSpan(wbrView, 3);
                lblFrom.Text = "From";
                btnSend.Text = "Close";
                this.messageIndex = messageIndex;
                txtSubject.ReadOnly = true;
            }
            else if (use == Enumerations.EmailPurpose.Send || use == Enumerations.EmailPurpose.Reply || use == Enumerations.EmailPurpose.Forward)
            {
                wbrView.Dispose();
                btnForward.Dispose();
                btnReply.Dispose();
                rtbWrite.TabIndex--;
                btnSend.TabIndex--;
                tlpMain.SetColumn(rtbWrite, 0);
                tlpMain.SetColumnSpan(rtbWrite, 3);
                this.fromIndexList = fromIndexList;
                //we need this for replying and forwarding
                this.messageIndex = messageIndex;
                lblFrom.Text = "To";
                btnSend.Text = "Send";
            }
            else if (use == Enumerations.EmailPurpose.Bcc || use == Enumerations.EmailPurpose.MassEmail)
            {
                wbrView.Dispose();
                btnForward.Dispose();
                btnReply.Dispose();
                rtbWrite.TabIndex--;
                btnSend.TabIndex--;
                tlpMain.SetColumn(rtbWrite, 0);
                tlpMain.SetColumnSpan(rtbWrite, 3);
                this.fromIndexList = fromIndexList;
                lblFrom.Text = "BCC";
                btnSend.Text = "Send";
            }
        }

        private void emailBrowser_Load(object sender, EventArgs e)
        {
            lvFromTo.LargeImageList = Program.home.instrumentSmall;
            //check what the purpose of this use of emailBrowser is

            int fromIndex;
            if (use == Enumerations.EmailPurpose.Receive)
            {

                //load the email
                ImapX.Message temp = clsStorage.currentClub.clubEmail.returnMessage(messageIndex);
                //we have the email downloaded, now display it

                fromIndex = clsStorage.currentClub.emailSearch(temp.From.Address);
                fromMember = fromIndex != -1;
                fromIndexList.Add(fromIndex);
                //search to see if the message is from a member
                //if it is, make the connection!
                lvFromTo.BeginUpdate();
                lvFromTo.Items.Clear();
                if (fromMember)
                    lvFromTo.Items.Add(new ListViewItem(new string[2]{clsStorage.currentClub.firstAndLastName(fromIndex), temp.From.Address},
                        Member.instrumentIconIndex(clsStorage.currentClub.members[fromIndex].curInstrument)));
                else
                {
                    if (String.IsNullOrEmpty(temp.From.DisplayName))
                        lvFromTo.Items.Add(new ListViewItem(new string[2] { temp.From.Address, temp.From.Address }));
                    else
                        lvFromTo.Items.Add(new ListViewItem(new string[2] { temp.From.DisplayName, temp.From.Address }));
                }
                lvFromTo.Refresh(); 
                lvFromTo.EndUpdate();
                

                txtSubject.Text = temp.Subject;
                string body = temp.Body.HasHtml ? temp.Body.Html : temp.Body.Text;
                wbrView.DocumentText = temp.Body.HasHtml ? body : WebUtility.HtmlEncode(body).Replace(Environment.NewLine, "<br />");
                if (wbrView.Document.Body != null)
                    wbrView.Document.Body.SetAttribute("scroll", "auto");
            }
            else if (use == Enumerations.EmailPurpose.Send ||
                use == Enumerations.EmailPurpose.Reply ||
                use == Enumerations.EmailPurpose.Forward ||
                use == Enumerations.EmailPurpose.Bcc ||
                use == Enumerations.EmailPurpose.MassEmail)
            {
                //load the from/to list
                lvFromTo.BeginUpdate();
                lvFromTo.Items.Clear();

                //go through the list of from/to people
                //if they are a real member, make the connection
                //otherwise, add them as an other
                if (use == Enumerations.EmailPurpose.Reply)
                {
                    //load the email
                    ImapX.Message temp = clsStorage.currentClub.clubEmail.returnMessage(messageIndex);

                    if (String.IsNullOrEmpty(temp.From.DisplayName))
                        lvFromTo.Items.Add(new ListViewItem(new string[2] { temp.From.Address, temp.From.Address }));
                    else
                        lvFromTo.Items.Add(new ListViewItem(new string[2] { temp.From.DisplayName, temp.From.Address }));
                }
                else if (use != Enumerations.EmailPurpose.MassEmail)
                {
                    foreach (int recipient in fromIndexList)
                    {
                        fromMember = recipient != -1;

                        if (fromMember)
                            lvFromTo.Items.Add(new ListViewItem(new string[2] { clsStorage.currentClub.firstAndLastName(recipient), clsStorage.currentClub.members[recipient].email },
                                Member.instrumentIconIndex(clsStorage.currentClub.members[recipient].curInstrument)));
                    }
                }


                lvFromTo.EndUpdate();

                //automatically fill subject line if we are forwarding or replying
                if (use == Enumerations.EmailPurpose.Reply || use == Enumerations.EmailPurpose.Forward)
                {
                    ImapX.Message temp = clsStorage.currentClub.clubEmail.returnMessage(messageIndex);
                    txtSubject.Text = Marimba.email.replySubject(temp.Subject, use);                   
                }
            }
        }

        private void btnSend_Click(object sender, EventArgs e)
        {
            ImapX.Message temp;
            //if we are viewing a message, this button closes the view
            if (use == Enumerations.EmailPurpose.Receive)
            {
                this.Close();
            }
            else
            {
                //send the message

                //first, take the rich text
                //then, run the code to turn it into HTML
                string strHTML = rtbWrite.Rtf;
                strHTML = RTFtoHTML.ConvertRtfToHtml(strHTML);

                //attach a signature if the user wants us to
                if (Properties.Settings.Default.attachSig)
                    strHTML += clsStorage.currentClub.clubEmail.createSignature();

                //next, check if we are replying or forwarding
                //in which case, attach that email here
                if (use == Enumerations.EmailPurpose.Reply || use == Enumerations.EmailPurpose.Forward)
                {
                    temp = clsStorage.currentClub.clubEmail.returnMessage(messageIndex);
                    string attachedMessage = temp.Body.HasHtml ? temp.Body.Html : temp.Body.Text;
                    attachedMessage = temp.Body.HasHtml ? attachedMessage : WebUtility.HtmlEncode(attachedMessage).Replace(Environment.NewLine, "<br />");

                    strHTML += Marimba.email.replyHeader(temp.From.Address, temp.From.DisplayName, temp.Date.Value, temp.To[0].DisplayName, temp.Subject, use) + attachedMessage;
                }

                //now call the necessary functions to send
                int iRecipients;
                string[] toAddress;
                string[] toNames;
                //if it is being sent to someone specific, then we need to process these names
                if (use == Enumerations.EmailPurpose.Send || use == Enumerations.EmailPurpose.Reply || use == Enumerations.EmailPurpose.Forward || use == Enumerations.EmailPurpose.Bcc)
                {
                    //prepare the variables
                    iRecipients = lvFromTo.Items.Count;
                    toAddress = new string[iRecipients];
                    toNames = new string[iRecipients];
                    //fill up these two arrays with the recipient information
                    for (int i = 0; i < iRecipients; i++)
                    {
                        toNames[i] = lvFromTo.Items[i].SubItems[0].Text;
                        toAddress[i] = lvFromTo.Items[i].SubItems[1].Text;
                    }

                    //we are good to send!

                    if (clsStorage.currentClub.clubEmail.sendMessage(toAddress, toNames, txtSubject.Text, strHTML, use))
                    {
                        if (Properties.Settings.Default.playSounds)
                            sound.success.Play();
                        MessageBox.Show("Successfully sent e-mail.", "E-Mail Sent");
                        this.Close();
                    }
                    else
                    {
                        if (Properties.Settings.Default.playSounds)
                            sound.error.Play();
                        MessageBox.Show("E-mail was not sent. Verify all the club e-mail information is correct and the the e-mail has recipients.", "Error Sending E-Mail");
                    }
                }
                //for the mass email, it's a big list!
                else
                {
                    IList<string> allAddress = new List<string>();
                    IList<string> allNames = new List<string>();
                    int badEmails = 0;

                    //go through and add members to be mailed to
                    for (int i = 0; i < clsStorage.currentClub.iMember; i++)
                    {
                        allAddress.Add(clsStorage.currentClub.members[i].email);
                        allNames.Add(clsStorage.currentClub.firstAndLastName(i));
                    }

                    //go through and remove bad emails addresses
                    badEmails = 0;
                    for (int i = 0; i < clsStorage.currentClub.iMember; i++)
                    {
                        //if blank, remove from this list
                        if (String.IsNullOrEmpty(allAddress[i - badEmails]))
                        {
                            allAddress.RemoveAt(i - badEmails);
                            allNames.RemoveAt(i - badEmails);
                            badEmails++;
                        }
                    }

                    //there is a 100 person limit, so we need to work around that
                    //so send a BCC list out every 100 people
                    int iEmails = allAddress.Count / 100 + 1;
                    int iLastMax = allAddress.Count - 100 * (iEmails - 1);
                    int currentMax;


                    for (int j = 0; j < iEmails; j++)
                    {
                        //for the last run, don't do the full 100; do only as many as needed
                        if (j == iEmails - 1)
                        {
                            toAddress = new string[iLastMax];
                            toNames = new string[iLastMax];
                            currentMax = iLastMax;
                        }
                        //for all other runs, do the full 100
                        else
                        {
                            toAddress = new string[100];
                            toNames = new string[100];
                            currentMax = 100;
                        }
                        //go through and add members to be mailed to
                        for (int i = j * 100; i < 100 + j * 100 && i < allAddress.Count; i++)
                        {
                            toAddress[i - 100 * j] = allAddress[i];
                            toNames[i - 100 * j] = allNames[i];
                        }

                        //we are good to send!

                        
                        if (!clsStorage.currentClub.clubEmail.sendMessage(toAddress, toNames, txtSubject.Text, strHTML, use))
                        {
                            if (Properties.Settings.Default.playSounds)
                                sound.error.Play();
                            MessageBox.Show("E-mail was not sent. Verify all the club e-mail information is correct.", "Error Sending E-Mail");
                            break;
                        }
                    }
                    if (Properties.Settings.Default.playSounds)
                        sound.success.Play();
                    MessageBox.Show("Successfully sent e-mail.", "E-Mail Sent");
                    this.Close();
                }
            }          
        }

        private void lvFromTo_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (lvFromTo.SelectedIndices[0] != -1 && fromMember) //something is selected
            {
                if (Properties.Settings.Default.playSounds)
                    sound.click.Play();
                //insert pop-up with member's profile
                Form memberprofile = new Profile(fromIndexList[lvFromTo.SelectedIndices[0]]);
                memberprofile.ShowDialog();
                memberprofile.Dispose();
            }
        }

        private void btnReply_Click(object sender, EventArgs e)
        {
            if (Properties.Settings.Default.playSounds)
                sound.click.Play();

            emailBrowser webDesign = new emailBrowser(Enumerations.EmailPurpose.Reply, messageIndex, fromIndexList);
            webDesign.Show();
        }

        private void btnForward_Click(object sender, EventArgs e)
        {
            //currently, forwarding is not implemented
            //it's not as useful and would take a lot of reworking of the way I did email
            //I might revisit this later
        }

        /// <summary>
        /// Returns array one index smaller without specified element at index
        /// </summary>
        /// <param name="array">Array</param>
        /// <param name="index">Index of parameter to remove</param>
        /// <returns></returns>
        private string[] removeElement(string[] array, int index)
        {
            int length = array.Length - 1;
            string[] output = new string[length];
            for (int i = 0; i < index; i++)
                output[i] = array[i];
            for (int i = index; i < length - 1; i++)
                output[i] = array[i + 1];
            return output;
        }
    }
}
