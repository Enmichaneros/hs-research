using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCoreAi.Meta;
using SabberStoneCoreAi.Nodes;
using SabberStoneCoreAi.Score;

namespace SabberStoneCoreAi
{

	internal class Program
	{
		private static readonly Random Rnd = new Random();

		static int populationSize = 1; // How many decks are in a single generation.
		static int generationLimit = 1; // How many generations to run in a single sitting.

		static double mutationChance = 0.05d; // The probability for a random new child deck to mutate.

		static List<Card> availableCards; // List of cards to choose from when creating / mutating a deck.
		static CardClass hero = CardClass.PALADIN; // The class used for all population decks.

		public class MemberDeck
		{
			public List<Card> deck;
			public int fitnessScore; // Represents how fit the deck is, usually by winrate.

			public MemberDeck(){
				deck = randomCards(30);
				fitnessScore = 0;
			}

			public void mutateDeck(){
				// if (random < mutationChance), mutate a random card in the deck.
				if (Rnd.NextDouble() < mutationChance){
					// Choose a card in the original deck to mutate.
					int oldCard = Rnd.Next(deck.Count);
					// Choose a new card to replace it with.
					Card newCard = randomCards(1)[1];

					// generate a new card until it's valid for this deck.
					int count = deck.Where(s=>s!=null && s.Equals(newCard)).Count();
					while (count < 2 && !newCard.Equals(deck[oldCard])) {
						newCard = randomCards(1)[1];
						count = deck.Where(s=>s!=null && s.Equals(newCard)).Count();
					}
					deck[oldCard] = newCard;
				}
			}

		}

		// Filter cards into the list of available cards
		public static void filterCards()
		{
			availableCards = new List<Card>()
			{
				Cards.FromName("Blessing of Might"),
				Cards.FromName("Blessing of Might"),
				Cards.FromName("Gnomish Inventor"),
				Cards.FromName("Gnomish Inventor"),
				Cards.FromName("Goldshire Footman"),
				Cards.FromName("Goldshire Footman"),
				Cards.FromName("Hammer of Wrath"),
				Cards.FromName("Hammer of Wrath"),
				Cards.FromName("Gnomish Inventor"),
				Cards.FromName("Gnomish Inventor"),
				Cards.FromName("Goldshire Footman"),
				Cards.FromName("Goldshire Footman"),
				Cards.FromName("Hammer of Wrath"),
				Cards.FromName("Hammer of Wrath"),
				Cards.FromName("Hand of Protection"),
				Cards.FromName("Hand of Protection"),
				Cards.FromName("Holy Light"),
				Cards.FromName("Holy Light"),
				Cards.FromName("Ironforge Rifleman"),
				Cards.FromName("Ironforge Rifleman"),
				Cards.FromName("Hand of Protection"),
				Cards.FromName("Hand of Protection"),
				Cards.FromName("Holy Light"),
				Cards.FromName("Holy Light"),
				Cards.FromName("Ironforge Rifleman"),
				Cards.FromName("Ironforge Rifleman"),
				Cards.FromName("Light's Justice"),
				Cards.FromName("Light's Justice"),
				Cards.FromName("Lord of the Arena"),
				Cards.FromName("Lord of the Arena"),
				Cards.FromName("Nightblade"),
				Cards.FromName("Nightblade"),
				Cards.FromName("Raid Leader"),
				Cards.FromName("Raid Leader"),
				Cards.FromName("Stonetusk Boar"),
				Cards.FromName("Stonetusk Boar"),
				Cards.FromName("Stormpike Commando"),
				Cards.FromName("Stormpike Commando"),
				Cards.FromName("Stormwind Champion"),
				Cards.FromName("Stormwind Champion"),
				Cards.FromName("Stormwind Knight"),
				Cards.FromName("Stormwind Knight")
			};
		}

		private static void Main(string[] args)
		{
			Console.WriteLine("Starting test setup.");
			filterCards();
			runGame();
			for (int i = 0; i < generationLimit; i++){
				for (int k = 0; k < populationSize; k++){}
			}
			//FullGame();
			//RandomGames();
			Console.WriteLine("Test end!");
		}

		public static List<Card> randomCards(int n)
		{
			List<Card> cards = new List<Card>();
			for (int i = 0; i < n; i++){
				int r = Rnd.Next(availableCards.Count);
				// Check that the cards you're sending are valid by themselves.
				int count = cards.Where(s=>s!=null && s.Equals(availableCards[r])).Count();
				if (count < 2) {
					cards.Add(availableCards[r]);
				}
				else{
					// If the card isn't usable, you haven't added to the deck yet.
					i--;
				}
			}
			return cards;
		}

		// Test function utilizing all the functions I'm implementing.
		public static void runGame()
		{
			MemberDeck player1 = new MemberDeck();
			MemberDeck player2 = new MemberDeck();
				var game = new Game(
				new GameConfig()
				{
					StartPlayer = 1,
					Player1Name = "FitzVonGerald",
					Player1HeroClass = hero,
					Player1Deck = player1.deck,
					Player2Name = "RehHausZuckFuchs",
					Player2HeroClass = hero,
					Player2Deck = player2.deck,
					FillDecks = false,
					Shuffle = true,
					SkipMulligan = false
				});
			game.StartGame();

			var aiPlayer1 = new AggroScore();
			var aiPlayer2 = new AggroScore();

			List<int> mulligan1 = aiPlayer1.MulliganRule().Invoke(game.Player1.Choice.Choices.Select(p => game.IdEntityDic[p]).ToList());
			List<int> mulligan2 = aiPlayer2.MulliganRule().Invoke(game.Player2.Choice.Choices.Select(p => game.IdEntityDic[p]).ToList());

			Console.WriteLine($"Player1: Mulligan {String.Join(",", mulligan1)}");
			Console.WriteLine($"Player2: Mulligan {String.Join(",", mulligan2)}");

			game.Process(ChooseTask.Mulligan(game.Player1, mulligan1));
			game.Process(ChooseTask.Mulligan(game.Player2, mulligan2));

			game.MainReady();

			while (game.State != State.COMPLETE)
			{
				Console.WriteLine("");
				Console.WriteLine($"Player1: {game.Player1.PlayState} / Player2: {game.Player2.PlayState} - " +
								  $"ROUND {(game.Turn + 1) / 2} - {game.CurrentPlayer.Name}");
				Console.WriteLine($"Hero[P1]: {game.Player1.Hero.Health} / Hero[P2]: {game.Player2.Hero.Health}");
				Console.WriteLine("");
				while (game.State == State.RUNNING && game.CurrentPlayer == game.Player1)
				{
					Console.WriteLine($"* Calculating solutions *** Player 1 ***");
					List<OptionNode> solutions = OptionNode.GetSolutions(game, game.Player1.Id, aiPlayer1, 10, 500);
					var solution = new List<PlayerTask>();
					solutions.OrderByDescending(p => p.Score).First().PlayerTasks(ref solution);
					Console.WriteLine($"- Player 1 - <{game.CurrentPlayer.Name}> ---------------------------");
					foreach (PlayerTask task in solution)
					{
						Console.WriteLine(task.FullPrint());
						game.Process(task);
						if (game.CurrentPlayer.Choice != null)
						{
							Console.WriteLine($"* Recaclulating due to a final solution ...");
							break;
						}
					}
				}

				// Random mode for Player 2
				Console.WriteLine($"- Player 2 - <{game.CurrentPlayer.Name}> ---------------------------");
				while (game.State == State.RUNNING && game.CurrentPlayer == game.Player2)
				{
					//var options = game.Options(game.CurrentPlayer);
					//var option = options[Rnd.Next(options.Count)];
					//Log.Info($"[{option.FullPrint()}]");
					//game.Process(option);
					Console.WriteLine($"* Calculating solutions *** Player 2 ***");
					List<OptionNode> solutions = OptionNode.GetSolutions(game, game.Player2.Id, aiPlayer2, 10, 500);
					var solution = new List<PlayerTask>();
					solutions.OrderByDescending(p => p.Score).First().PlayerTasks(ref solution);
					Console.WriteLine($"- Player 2 - <{game.CurrentPlayer.Name}> ---------------------------");
					foreach (PlayerTask task in solution)
					{
						Console.WriteLine(task.FullPrint());
						game.Process(task);
						if (game.CurrentPlayer.Choice != null)
						{
							Console.WriteLine($"* Recaclulating due to a final solution ...");
							break;
						}
					}
				}
			}
			Console.WriteLine($"Game: {game.State}, Player1: {game.Player1.PlayState} / Player2: {game.Player2.PlayState}");
		}


		public static void RandomGames()
		{
			int total = 1000;
			var watch = Stopwatch.StartNew();

			var gameConfig = new GameConfig()
			{
				StartPlayer = -1,
				Player1Name = "FitzVonGerald",
				Player1HeroClass = CardClass.PALADIN,
				Player1Deck = new List<Card>()
						{
						Cards.FromName("Blessing of Might"),
						Cards.FromName("Blessing of Might"),
						Cards.FromName("Gnomish Inventor"),
						Cards.FromName("Gnomish Inventor"),
						Cards.FromName("Goldshire Footman"),
						Cards.FromName("Goldshire Footman"),
						Cards.FromName("Hammer of Wrath"),
						Cards.FromName("Hammer of Wrath"),
						Cards.FromName("Hand of Protection"),
						Cards.FromName("Hand of Protection"),
						Cards.FromName("Holy Light"),
						Cards.FromName("Holy Light"),
						Cards.FromName("Ironforge Rifleman"),
						Cards.FromName("Ironforge Rifleman"),
						Cards.FromName("Light's Justice"),
						Cards.FromName("Light's Justice"),
						Cards.FromName("Lord of the Arena"),
						Cards.FromName("Lord of the Arena"),
						Cards.FromName("Nightblade"),
						Cards.FromName("Nightblade"),
						Cards.FromName("Raid Leader"),
						Cards.FromName("Raid Leader"),
						Cards.FromName("Stonetusk Boar"),
						Cards.FromName("Stonetusk Boar"),
						Cards.FromName("Stormpike Commando"),
						Cards.FromName("Stormpike Commando"),
						Cards.FromName("Stormwind Champion"),
						Cards.FromName("Stormwind Champion"),
						Cards.FromName("Stormwind Knight"),
						Cards.FromName("Stormwind Knight")
						},
				Player2Name = "RehHausZuckFuchs",
				Player2HeroClass = CardClass.PALADIN,
				Player2Deck = new List<Card>()
						{
						Cards.FromName("Blessing of Might"),
						Cards.FromName("Blessing of Might"),
						Cards.FromName("Gnomish Inventor"),
						Cards.FromName("Gnomish Inventor"),
						Cards.FromName("Goldshire Footman"),
						Cards.FromName("Goldshire Footman"),
						Cards.FromName("Hammer of Wrath"),
						Cards.FromName("Hammer of Wrath"),
						Cards.FromName("Hand of Protection"),
						Cards.FromName("Hand of Protection"),
						Cards.FromName("Holy Light"),
						Cards.FromName("Holy Light"),
						Cards.FromName("Ironforge Rifleman"),
						Cards.FromName("Ironforge Rifleman"),
						Cards.FromName("Light's Justice"),
						Cards.FromName("Light's Justice"),
						Cards.FromName("Lord of the Arena"),
						Cards.FromName("Lord of the Arena"),
						Cards.FromName("Nightblade"),
						Cards.FromName("Nightblade"),
						Cards.FromName("Raid Leader"),
						Cards.FromName("Raid Leader"),
						Cards.FromName("Stonetusk Boar"),
						Cards.FromName("Stonetusk Boar"),
						Cards.FromName("Stormpike Commando"),
						Cards.FromName("Stormpike Commando"),
						Cards.FromName("Stormwind Champion"),
						Cards.FromName("Stormwind Champion"),
						Cards.FromName("Stormwind Knight"),
						Cards.FromName("Stormwind Knight")
						},
				FillDecks = false,
				Shuffle = true,
				SkipMulligan = false,
				Logging = false,
				History = false
			};

			int turns = 0;
			int[] wins = new[] { 0, 0 };
			for (int i = 0; i < total; i++)
			{
				var game = new Game(gameConfig);
				game.StartGame();

				game.Process(ChooseTask.Mulligan(game.Player1, new List<int>{}));
				game.Process(ChooseTask.Mulligan(game.Player2, new List<int>{}));

				game.MainReady();

				while (game.State != State.COMPLETE)
				{
					List<PlayerTask> options = game.CurrentPlayer.Options();
					PlayerTask option = options[Rnd.Next(options.Count)];
					//Console.WriteLine(option.FullPrint());
					game.Process(option);
				}
				turns += game.Turn;
				if (game.Player1.PlayState == PlayState.WON)
					wins[0]++;
				if (game.Player2.PlayState == PlayState.WON)
					wins[1]++;

			}
			watch.Stop();

			Console.WriteLine($"{total} games with {turns} turns took {watch.ElapsedMilliseconds} ms => " +
							  $"Avg. {watch.ElapsedMilliseconds / total} per game " +
							  $"and {watch.ElapsedMilliseconds / (total * turns)} per turn!");
			Console.WriteLine($"playerA {wins[0] * 100 / total}% vs. playerB {wins[1] * 100 / total}%!");
		}

		public static void FullGame()
		{
			var game = new Game(
				new GameConfig()
				{
					StartPlayer = 1,
					Player1Name = "FitzVonGerald",
					Player1HeroClass = CardClass.WARRIOR,
					Player1Deck = Decks.AggroPirateWarrior,
					Player2Name = "RehHausZuckFuchs",
					Player2HeroClass = CardClass.WARRIOR,
					Player2Deck = Decks.AggroPirateWarrior,
					FillDecks = false,
					Shuffle = true,
					SkipMulligan = false
				});
			game.StartGame();

			var aiPlayer1 = new AggroScore();
			var aiPlayer2 = new AggroScore();

			List<int> mulligan1 = aiPlayer1.MulliganRule().Invoke(game.Player1.Choice.Choices.Select(p => game.IdEntityDic[p]).ToList());
			List<int> mulligan2 = aiPlayer2.MulliganRule().Invoke(game.Player2.Choice.Choices.Select(p => game.IdEntityDic[p]).ToList());

			Console.WriteLine($"Player1: Mulligan {String.Join(",", mulligan1)}");
			Console.WriteLine($"Player2: Mulligan {String.Join(",", mulligan2)}");

			game.Process(ChooseTask.Mulligan(game.Player1, mulligan1));
			game.Process(ChooseTask.Mulligan(game.Player2, mulligan2));

			game.MainReady();

			while (game.State != State.COMPLETE)
			{
				Console.WriteLine("");
				Console.WriteLine($"Player1: {game.Player1.PlayState} / Player2: {game.Player2.PlayState} - " +
								  $"ROUND {(game.Turn + 1) / 2} - {game.CurrentPlayer.Name}");
				Console.WriteLine($"Hero[P1]: {game.Player1.Hero.Health} / Hero[P2]: {game.Player2.Hero.Health}");
				Console.WriteLine("");
				while (game.State == State.RUNNING && game.CurrentPlayer == game.Player1)
				{
					Console.WriteLine($"* Calculating solutions *** Player 1 ***");
					List<OptionNode> solutions = OptionNode.GetSolutions(game, game.Player1.Id, aiPlayer1, 10, 500);
					var solution = new List<PlayerTask>();
					solutions.OrderByDescending(p => p.Score).First().PlayerTasks(ref solution);
					Console.WriteLine($"- Player 1 - <{game.CurrentPlayer.Name}> ---------------------------");
					foreach (PlayerTask task in solution)
					{
						Console.WriteLine(task.FullPrint());
						game.Process(task);
						if (game.CurrentPlayer.Choice != null)
						{
							Console.WriteLine($"* Recaclulating due to a final solution ...");
							break;
						}
					}
				}

				// Random mode for Player 2
				Console.WriteLine($"- Player 2 - <{game.CurrentPlayer.Name}> ---------------------------");
				while (game.State == State.RUNNING && game.CurrentPlayer == game.Player2)
				{
					//var options = game.Options(game.CurrentPlayer);
					//var option = options[Rnd.Next(options.Count)];
					//Log.Info($"[{option.FullPrint()}]");
					//game.Process(option);
					Console.WriteLine($"* Calculating solutions *** Player 2 ***");
					List<OptionNode> solutions = OptionNode.GetSolutions(game, game.Player2.Id, aiPlayer2, 10, 500);
					var solution = new List<PlayerTask>();
					solutions.OrderByDescending(p => p.Score).First().PlayerTasks(ref solution);
					Console.WriteLine($"- Player 2 - <{game.CurrentPlayer.Name}> ---------------------------");
					foreach (PlayerTask task in solution)
					{
						Console.WriteLine(task.FullPrint());
						game.Process(task);
						if (game.CurrentPlayer.Choice != null)
						{
							Console.WriteLine($"* Recaclulating due to a final solution ...");
							break;
						}
					}
				}
			}
			Console.WriteLine($"Game: {game.State}, Player1: {game.Player1.PlayState} / Player2: {game.Player2.PlayState}");
		}

	}
}
