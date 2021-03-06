﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Windows.Forms;
using Fizzi.Libraries.ChallongeApiWrapper;

namespace SkillKeeper
{
    public partial class SKChallongeImporter : Form
    {
        private ChallongePortal portal;

        private List<Tournament> completedTournaments = new List<Tournament>();
        private List<Match> importMatches = new List<Match>();
        private List<Person> importPlayers = new List<Person>();
        private List<Person> currentPlayers = new List<Person>();
        private List<String> playerNames = new List<String>();
        private List<ImportPlayer> challongePlayerList = new List<ImportPlayer>();
        private List<String> eventList = new List<String>();
        private String tournamentName = "";

        private List<String> addAlts = new List<String>();

        private Tournament curTourney = new Tournament();

        public SKChallongeImporter()
        {
            InitializeComponent();
            sKLinkDataGridViewTextBoxColumn.DataSource = playerNames;
            playerNames.Add("<< Create New Player >>");
        }

        public void importChallonge(String apiKey, String subDomain, List<Person> playerList)
        {
            if (subDomain != null && subDomain.Length > 0)
                portal = new ChallongePortal(apiKey, subDomain);
            else
                portal = new ChallongePortal(apiKey);

            //Fetch all tournaments that have been completed ahead of time and save into local variable
            completedTournaments = portal.GetTournaments().Where(t => t.CompletedAt.HasValue).ToList();

            //Fetch name of first tournament in list or leave blank if no tournaments
            tournamentName = completedTournaments.Select(t => t.Name).FirstOrDefault();

            //Load current players in leaderboard
            foreach (Person p in playerList)
            {
                currentPlayers.Add(p);
                playerNames.Add(p.Name);
            }
            playerNames.Sort();

            //Display all available tournaments
            foreach (Tournament t in completedTournaments)
            {
                eventList.Add(t.Name);
            }
            eventSelector.DataSource = eventList;
        }

        // Update the list of players found in the selected event.
        private void updatePlayerList()
        {
            eventDatePicker.Value = curTourney.CompletedAt.Value;

            challongePlayerList.Clear();
            foreach (Participant p in portal.GetParticipants(curTourney.Id))
            {
                ImportPlayer ip = new ImportPlayer();
                ip.ID = p.Id.ToString();
                ip.Name = p.NameOrUsername;
                ip.SKLink = getMatch(p.NameOrUsername);

                challongePlayerList.Add(ip);
            }

            importPlayerBindingSource.DataSource = new BindingList<ImportPlayer>(challongePlayerList);
        }

        // Update the associated import player whenever a link is manually set.
        private void importPlayerList_CurrentCellDirtyStateChanged(object sender, EventArgs e)
        {
            foreach (ImportPlayer p in challongePlayerList)
            {
                if (p.ID == (String) importPlayerList.CurrentRow.Cells[0].Value)
                {
                    p.SKLink = (String) importPlayerList.CurrentRow.Cells[2].Value;
                }
            }
        }

        // Method to attempt to find a match between a player in the TIO file and a player already in the World. If it cannot find a player with the same name,
        // it then attempts to search through the lists of alternate names for each player.
        private String getMatch(String playerName)
        {
            String result = "<< Create New Player >>";
            Boolean foundMatch = false;
            foreach (Person person in currentPlayers)
            {
                if (person.Name.ToUpper() == playerName.ToUpper() || (playerName.ToUpper().StartsWith(person.Team.ToUpper()) && playerName.ToUpper().EndsWith(person.Name.ToUpper()) && person.Name.Length > 0))
                {
                    foundMatch = true;
                    result = person.Name;
                    break;
                }
            }
            if (!foundMatch)
            {
                foreach (Person person in currentPlayers)
                {
                    foreach (String altName in person.Alts)
                    {
                        if (altName.ToUpper() == playerName.ToUpper() || (playerName.ToUpper().StartsWith(person.Team.ToUpper()) && playerName.ToUpper().EndsWith(altName.ToUpper()) && altName.Length > 0))
                        {
                            result = person.Name;
                        }
                    }
                }
            }

            return result;
        }

        // Rebuild the list of players in the event whenever the event selector is used.
        private void eventSelector_SelectedIndexChanged(object sender, EventArgs e)
        {
            //Fetch selected tourney
            curTourney = completedTournaments.FirstOrDefault(t => t.Name == eventSelector.Text);

            updatePlayerList();
        }

        // Cancel
        private void cancelButton_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        // Confirm Import. Process all matches in selected event.
        private void importButton_Click(object sender, EventArgs e)
        {
            foreach (ImportPlayer p in challongePlayerList)
            {
                if (p.SKLink == "<< Create New Player >>")
                {
                    if (!playerNames.Contains(p.Name))
                    {
                        Person person = new Person();
                        person.Name = p.Name;
                        importPlayers.Add(person);
                    }
                }
                else if (p.SKLink != p.Name)
                {
                    addAlts.Add(p.SKLink + "\t" + p.Name);
                }
            }
            
            foreach (Fizzi.Libraries.ChallongeApiWrapper.Match m in portal.GetMatches(curTourney.Id))
            {
                if (m.State == "complete")
                {
                    createMatch(m.Player1Id.ToString(), m.Player2Id.ToString(), m.WinnerId.ToString());
                }
            }

            this.Close();
        }

        // Create a match out of XML data. If not grand finals, add it immediately to the list of matches to import. Otherwise, store for adding at the end.
        private void createMatch(String p1ID, String p2ID, String WinnerID)
        {
            Match m = new Match();

            m.Description = tournamentName + " - " + eventSelector.Text;
            m.Timestamp = eventDatePicker.Value;

            foreach (ImportPlayer p in challongePlayerList)
            {
                if (p.ID == p1ID)
                {
                    if (p.SKLink == "<< Create New Player >>")
                        m.Player1 = p.Name;
                    else
                        m.Player1 = p.SKLink;
                }
                if (p.ID == p2ID)
                {
                    if (p.SKLink == "<< Create New Player >>")
                        m.Player2 = p.Name;
                    else
                        m.Player2 = p.SKLink;
                }
            }

            if (m.Player1 != null && m.Player2 != null)
            {
                if (p1ID == WinnerID)
                    m.Winner = 1;
                else
                    m.Winner = 2;

                m.ID = Guid.NewGuid().ToString("N");
               
                importMatches.Add(m);
            }
        }

        public List<Person> getImportPlayers()
        {
            return importPlayers;
        }

        public List<Match> getImportMatches()
        {
            return importMatches;
        }

        public List<String> getNewAlts()
        {
            return addAlts;
        }
    }
}
