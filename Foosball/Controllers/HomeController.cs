using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Web.Mvc;

namespace Foosball.Controllers
{
    public class HomeController : Controller
    {
        private static Dictionary<string, string> _playerDictionary;

        public Dictionary<string, string> GetPlayerDictionary
        {
            get
            {
                if (_playerDictionary == null)
                {
                    _playerDictionary = new Dictionary<string, string>();
                    string[] array = System.IO.File.ReadAllLines(Server.MapPath(@"~/App_Data/Players_Map.txt"));
                    foreach (string[] maps in from line in array where !string.IsNullOrWhiteSpace(line) select line.Split(','))
                    {
                        _playerDictionary.Add(maps[0], maps[1]);
                    }
                }
                return _playerDictionary;
            }
        }

        public ActionResult Index()
        {
            List<UserRank> userRanks = GetUserRanks();
            return View(userRanks);
        }

        private string[] GetDataInJsonString()
        {
            return System.IO.File.ReadAllLines(Server.MapPath(@"~/App_Data/Data.txt"));
        }

        private List<Match> GetAllMatches()
        {
            return TransformNewStringToObject(GetDataInJsonString());
        }

        private List<Match> GetAllMatches(string username)
        {
            return
                GetAllMatches()
                    .Where(x => x.Team1.Players.Contains(username) || x.Team2.Players.Contains(username))
                    .ToList();
        }
        
        private List<Match> TransformNewStringToObject(string[] lines)
        {
            List<Match> matches = new List<Match>();
            foreach (string matchStr in lines)
            {
                if (string.IsNullOrWhiteSpace(matchStr) || matchStr.StartsWith("(U)"))
                    continue;

                int dateS = matchStr.IndexOf('[');
                int dateE = matchStr.IndexOf(']');
                int score = matchStr.LastIndexOf('-');

                string scoreStr = matchStr.Substring(score + 1).Trim();
                string dateStr = matchStr.Substring(dateS + 1, dateE - dateS - 1).Trim();
                string players = matchStr.Substring(dateE + 1, score - dateE - 1).Trim();
                string[] teams = players.Split(new[] { " vs " }, StringSplitOptions.None);

                Match match = new Match
                {
                    Team1 = new Team
                    {
                        Players = teams[0].Split(',').ToList().Select(x => x.Trim()).ToList()
                    },
                    Team2 = new Team
                    {
                        Players = teams[1].Split(',').ToList().Select(x => x.Trim()).ToList()
                    },
                    Date = DateTime.Parse(dateStr),
                    Score = scoreStr
                };
                matches.Add(match);
            }
            return matches;
        }

        public ActionResult User(string q)
        {
            int rank;
            int bestWinStreak;
            int worstLosingtreak;
            UserProfile user = new UserProfile
            {
                Name = q,
                UserRank = GetUserRank(q, out rank),
                BestMatch = GetBestWonMatch(q, out bestWinStreak),
                WorstMatch = GetWorstLostMatch(q, out worstLosingtreak),
                Rank = rank,
                BestWinStreak = bestWinStreak,
                WortsLosingStreak = worstLosingtreak,
                RecentMatches = GetAllMatches(q) // OrderByDescending( x => x.Date).ToList()
            };
            user.RecentMatches.Reverse();
            return View(user);
        }

        public ActionResult RecentMatches()
        {
            List<Match> matches = GetAllMatches();
            matches.Reverse();
            return View(matches);
        }

        private List<UserRank> GetUserRanks()
        {
            List<Match> allMatches = GetAllMatches();
            List<UserRank> userRanks = GetRankings(allMatches);
            return userRanks;
        }

        private List<Match> GetAllWonMatches(string username, out Match bestWin, out int bestWinStreak)
        {
            List<Match> matches = GetAllMatches(username);
            List<Match> wonMatches = new List<Match>();
            int maxGoalDifference = 0;
            bestWinStreak = 0;
            int currentWinStreak = 0;
            bestWin = null;
            foreach (Match match in matches)
            {
                int factor = 1;
                if (match.Team2.Players.Contains(username))
                    factor = factor * -1;

                List<string> score = match.Score.Split(':').ToList();
                int goalDifference = int.Parse(score[0]) - int.Parse(score[1]);
                goalDifference = goalDifference * factor;

                if (goalDifference > -1)
                {
                    currentWinStreak++;

                    if (currentWinStreak > bestWinStreak)
                        bestWinStreak = currentWinStreak;

                    wonMatches.Add(match);
                    if (goalDifference > maxGoalDifference)
                    {
                        maxGoalDifference = goalDifference;
                        bestWin = match;
                    }
                }
                else
                {
                    currentWinStreak = 0;
                }
            }
            return wonMatches;
        }
        private List<Match> GetAllDefeatedMatches(string username, out Match worstDefeat, out int worstLosingStreak)
        {
            List<Match> matches = GetAllMatches(username);
            List<Match> defeatedMatches = new List<Match>();
            int maxGoalDifference = 0;
            worstLosingStreak = 0;
            int currentlosingStreak = 0;
            worstDefeat = null;
            foreach (Match match in matches)
            {
                int factor = 1;
                if (match.Team1.Players.Contains(username))
                    factor = factor * -1;

                List<string> score = match.Score.Split(':').ToList();
                int goalDifference = int.Parse(score[0]) - int.Parse(score[1]);
                goalDifference = goalDifference * factor;

                if (goalDifference > -1)
                {
                    currentlosingStreak++;

                    if (currentlosingStreak > worstLosingStreak)
                        worstLosingStreak = currentlosingStreak;

                    defeatedMatches.Add(match);
                    if (goalDifference > maxGoalDifference)
                    {
                        maxGoalDifference = goalDifference;
                        worstDefeat = match;
                    }
                }
                else
                {
                    currentlosingStreak = 0;
                }
            }
            return defeatedMatches;
        }

        private UserRank GetUserRank(string username, out int rank)
        {
            List<UserRank> userRanks = GetUserRanks();
            UserRank user = userRanks.FirstOrDefault(x => x.Username.ToLower().Equals(username.ToLower()));
            rank = userRanks.IndexOf(user) + 1;
            return user;
        }

        private List<UserRank> GetRankings(List<Match> allMatches)
        {
            Hashtable hashtable = new Hashtable();
            foreach (Match match in allMatches)
            {
                List<string> matchPlayers = match.Team1.Players.Concat(match.Team2.Players).ToList();
                foreach (string player in matchPlayers)
                {
                    if (!hashtable.ContainsKey(player))
                        hashtable.Add(player, new UserRank());

                    UserRank userRank = (UserRank)hashtable[player];
                    userRank.Played = userRank.Played + 1;
                    hashtable[player] = userRank;
                }

                List<string> score = match.Score.Split(':').ToList();

                bool hasTeam1Won = int.Parse(score[0]) > int.Parse(score[1]);
                int team1GoalDifference = int.Parse(score[0]) - int.Parse(score[1]);

                bool hasTeam2Won = int.Parse(score[1]) > int.Parse(score[0]);
                int team2GoalDifference = int.Parse(score[1]) - int.Parse(score[0]);

                UpdateScoreTables(hashtable, match.Team1.Players, hasTeam1Won, team1GoalDifference, match.Date);
                UpdateScoreTables(hashtable, match.Team2.Players, hasTeam2Won, team2GoalDifference, match.Date);
            }

            List<UserRank> userRanks = new List<UserRank>();
            foreach (string key in hashtable.Keys)
            {
                string displayUsername;
                UserRank temp = (UserRank)hashtable[key];
                temp.Username = key;
                GetPlayerDictionary.TryGetValue(key, out displayUsername);
                temp.DisplayUsername = displayUsername;
                userRanks.Add(temp);
            }
            userRanks = userRanks
                        .OrderByDescending(x => x.QualityScore)
                        .ThenByDescending(x => x.GoalDifference)
                        //.OrderBy(x => x.GoalDifference)
                        .ToList();
            return userRanks;
        }

        private Hashtable UpdateScoreTables(Hashtable hashtable, List<string> players, bool won, int goalDifference, DateTime matchDate)
        {
            foreach (string player in players)
            {
                UserRank userRank = (UserRank)hashtable[player];
                if (userRank.Trend == null)
                    userRank.Trend = new List<string>();

                if (userRank.Rating == 0)
                    userRank.Rating = 100;

                if (won)
                {
                    userRank.Won = userRank.Won + 1;
                    userRank.Trend.Add("W");
                    userRank.Rating = userRank.Rating + 3;

                    if (goalDifference >= 3)
                        userRank.Rating = userRank.Rating + 1;
                    if (goalDifference >= 6)
                        userRank.Rating = userRank.Rating + 1;
                    if (goalDifference >= 9)
                        userRank.Rating = userRank.Rating + 1;
                }
                else
                {
                    userRank.Lost = userRank.Lost + 1;
                    userRank.Trend.Add("L");
                    userRank.Rating = userRank.Rating - 1;

                    if (goalDifference >= 3)
                        userRank.Rating = userRank.Rating - 1;
                    if (goalDifference >= 6)
                        userRank.Rating = userRank.Rating - 1;
                    if (goalDifference >= 9)
                        userRank.Rating = userRank.Rating - 1;
                }

                if (matchDate >= DateTime.Now.AddDays(-7))
                {
                    if (won) userRank.Scores[0].Sum += 10 * (1 + (0.25 * goalDifference));
                    userRank.Scores[0].Count++;
                }
                else if (matchDate >= DateTime.Now.AddDays(-14))
                {
                    if (won) userRank.Scores[1].Sum += 10 * (1 + (0.25 * goalDifference));
                    userRank.Scores[1].Count++;
                }
                else if (matchDate >= DateTime.Now.AddDays(-21))
                {
                    if (won) userRank.Scores[2].Sum += 10 * (1 + (0.25 * goalDifference));
                    userRank.Scores[2].Count++;
                }
                else if (matchDate >= DateTime.Now.AddDays(-28))
                {
                    if (won) userRank.Scores[3].Sum += 10 * (1 + (0.25 * goalDifference));
                    userRank.Scores[3].Count++;
                }
                else if (matchDate >= DateTime.Now.AddDays(-35))
                {
                    if (won) userRank.Scores[4].Sum += 10 * (1 + (0.25 * goalDifference));
                    userRank.Scores[4].Count++;
                }
                else
                {
                    if (won) userRank.Scores[5].Sum += 10 * (1 + (0.25 * goalDifference));
                    userRank.Scores[5].Count++;
                }
                userRank.GoalDifference = userRank.GoalDifference + goalDifference;
                hashtable[player] = userRank;
            }
            return hashtable;
        }

        private Match GetBestWonMatch(string username, out int winStreak)
        {
            Match best;
            GetAllWonMatches(username, out best, out winStreak);
            return best;
        }

        private Match GetWorstLostMatch(string username, out int losingStreak)
        {
            Match worst;
            GetAllDefeatedMatches(username, out worst, out losingStreak);
            return worst;
        }
    }

    public class Match
    {
        public Team Team1 { get; set; }
        public Team Team2 { get; set; }
        public string Score { get; set; }

        public DateTime Date { get; set; }

        public int GoalDifference
        {
            get
            {
                string[] scores = Score.Split(':');
                int team1Score = int.Parse(scores[0]),
                    team2Score = int.Parse(scores[1]);
                return team1Score == 10 ? team1Score - team2Score : team2Score - team1Score;
            }
        }
    }

    public class Team
    {
        public List<string> Players { get; set; }
    }

    public class ScoreTable
    {
        public ScoreTable()
        {
            Count = 0;
            Sum = 0d;
        }

        public int Count { get; set; }
        public double Sum { get; set; }
    }

    public class UserRank
    {
        public string DisplayUsername { get; set; }
        public string Username { get; set; }
        public int Played { get; set; }
        public int Won { get; set; }
        public int Lost { get; set; }
        public List<string> Trend { get; set; }
        public int GoalDifference { get; set; }

        private ScoreTable[] _scores;
        public ScoreTable[] Scores
        {
            get
            {
                if (_scores == null)
                {
                    _scores = new ScoreTable[6];
                    for (int i = 0; i < _scores.Length; i++)
                    {
                        _scores[i] = new ScoreTable();
                    }
                }

                return _scores;
            }
            set { _scores = value; }
        }

        public double QualityScore
        {
            get
            {
                if (_scores == null)
                    return 0d;

                double score = 
                               AdjustOffsetRating(1.0, _scores[0].Count) * (_scores[0].Sum / (_scores[0].Count == 0 ? 1 : _scores[0].Count))
                             + AdjustOffsetRating(0.8, _scores[1].Count) * (_scores[1].Sum / (_scores[1].Count == 0 ? 1 : _scores[1].Count))
                             + AdjustOffsetRating(0.6, _scores[2].Count) * (_scores[2].Sum / (_scores[2].Count == 0 ? 1 : _scores[2].Count))
                             + AdjustOffsetRating(0.4, _scores[3].Count) * (_scores[3].Sum / (_scores[3].Count == 0 ? 1 : _scores[3].Count))
                             + AdjustOffsetRating(0.2, _scores[4].Count) * (_scores[4].Sum / (_scores[4].Count == 0 ? 1 : _scores[4].Count))
                             + AdjustOffsetRating(0.1, _scores[5].Count) * (_scores[5].Sum / (_scores[5].Count == 0 ? 1 : _scores[5].Count));
                return score;
            }
        }

        public decimal Ratio
        {
            get { return (decimal) Won/Lost; }
        }

        public int Rating { get; set; }

        private double AdjustOffsetRating(double offset, int count)
        {
            if (ConfigurationManager.AppSettings["matchCountOffsetReductionStatus"].ToLower().Equals("enabled"))
            {
                if (count < 5)
                    return offset > 0.6 ? offset - 0.6 : 0.1;
                if (count < 10)
                    return offset > 0.4 ? offset - 0.4 : 0.1;
                if (count < 15)
                    return offset > 0.2 ? offset - 0.2 : 0.1;
            }

            return offset;
        }
    }

    public class UserProfile
    {
        public string Name { get; set; }
        public UserRank UserRank { get; set; }
        public Match BestMatch { get; set; }
        public Match WorstMatch { get; set; }
        public int Rank { get; set; }
        public int BestWinStreak { get; set; }
        public int WortsLosingStreak { get; set; }

        public List<Match> RecentMatches { get; set; }
    }
}
