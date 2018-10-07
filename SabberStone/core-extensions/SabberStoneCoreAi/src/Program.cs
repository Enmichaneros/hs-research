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

		static int populationSize = 3; // How many decks are in a single generation.
		static int generationLimit = 2; // How many generations to run in a single sitting.

		static int breedingPoolSize = 2;
		static double mutationChance = 0.05d; // The probability for a random new child deck to mutate.

		static List<Card> allCards;
		static List<Card> availableCards; // List of cards to choose from when creating / mutating a deck.
		static CardClass hero = CardClass.PALADIN; // The class used for all population decks.

		static List<MemberDeck> population;

		public class MemberDeck
		{
			public List<Card> deck;
			public double fitnessScore; // Represents how fit the deck is, usually by winrate.

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
					while (!newCard.Equals(deck[oldCard]) && ((newCard.Rarity == Rarity.LEGENDARY && count > 0) || count > 1)) {
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
			allCards = new List<Card>();
			foreach (Card card in Cards.All)
			{
				if (!allCards.Contains(card)
                    && card.Implemented
                    && card.Collectible
                    && card.Set == CardSet.CORE || card.Set == CardSet.EXPERT1
                    && card.Type != CardType.HERO
                    && card.Type != CardType.ENCHANTMENT
                    && card.Type != CardType.INVALID
                    && card.Type != CardType.HERO_POWER
                    && card.Type != CardType.TOKEN
                    )
                {
                    allCards.Add(card);
                }
			}
			availableCards = new List<Card>();
			foreach (Card card in allCards)
			{
				if (card.Class == CardClass.NEUTRAL || card.Class ==  hero)
                {
                    availableCards.Add(card);
                }
			}
		}

		private static void Main(string[] args)
		{
			Console.WriteLine("Starting test setup.");
			// initialize available cards and generate initial population
			filterCards();
			population = new List<MemberDeck>();
			for (int k = 0; k < populationSize; k++){
				MemberDeck deck = new MemberDeck();
				population.Add(deck);
			}

			int currentGeneration = 1;
			while (currentGeneration <= generationLimit){
				foreach (MemberDeck currentDeck in population){
					currentDeck.fitnessScore = EvaluateFitness(currentDeck.deck);
				}

				double maxFitnessScore = population[0].fitnessScore;
				// this is where we should create the breeding pool yea?
				List<MemberDeck> breedingPool = new List<MemberDeck>();
				for (int i = 0; i < populationSize; i++){
					Console.WriteLine($"Fitness of deck {i}: {population[i].fitnessScore}");
					if (breedingPool.Count == 0)
						breedingPool.Add(population[i]);
					else if (population[i].fitnessScore > breedingPool[breedingPool.Count - 1].fitnessScore){
						int j = breedingPoolSize - 1;
						for (; j >= 0; j--){
							Console.WriteLine($"Checking top fitness: {j}");
							if (j == 0 || population[i].fitnessScore < breedingPool[j].fitnessScore)
								break;
						}
						breedingPool.Insert(j, population[i]);
						if (breedingPool.Count > breedingPoolSize)
						breedingPool.RemoveAt(breedingPoolSize - 1);
					}
				}

				Console.WriteLine($"Best fitness: {breedingPool[0].fitnessScore}");
				Console.WriteLine("Composition of best deck: ----------------------");
				for (int i = 0; i < 30; i++){
					Console.WriteLine($"{breedingPool[0].deck[i].ToString()}");
				}

				currentGeneration++;
				if (currentGeneration > generationLimit)
					break;
				else{
					// generate new population
					// create a pool from the best X decks

				}
			}
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
					Player2HeroClass = CardClass.WARRIOR,
					Player2Deck = Decks.AggroPirateWarrior,
					FillDecks = false,
					Shuffle = true,
					SkipMulligan = false
				});
			game.StartGame();

			var aiPlayer1 = new ControlScore();
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

		public static double EvaluateFitness(List<Card> deck)
		{
			int total = 1;
			var watch = Stopwatch.StartNew();

			var gameConfig = new GameConfig()
			{
				StartPlayer = 1,
				Player1Name = "P1",
				Player1HeroClass = hero,
				Player1Deck = deck,
				Player2Name = "P2",
				Player2HeroClass = CardClass.WARRIOR,
				Player2Deck = Decks.AggroPirateWarrior,
				FillDecks = false,
				Shuffle = true,
				SkipMulligan = false,
				Logging = false,
				History = false
			};

			int[] wins = new[] { 0, 0 };
			for (int i = 0; i < total; i++)
			{
				var game = new Game(gameConfig);
				game.StartGame();

				var aiPlayer1 = new AggroScore();
				var aiPlayer2 = new AggroScore();

				List<int> mulligan1 = aiPlayer1.MulliganRule().Invoke(game.Player1.Choice.Choices.Select(p => game.IdEntityDic[p]).ToList());
				List<int> mulligan2 = aiPlayer2.MulliganRule().Invoke(game.Player2.Choice.Choices.Select(p => game.IdEntityDic[p]).ToList());

				// Console.WriteLine($"Player1: Mulligan {String.Join(",", mulligan1)}");
				// Console.WriteLine($"Player2: Mulligan {String.Join(",", mulligan2)}");

				game.Process(ChooseTask.Mulligan(game.Player1, mulligan1));
				game.Process(ChooseTask.Mulligan(game.Player2, mulligan2));

				game.MainReady();
				try
				{
					while (game.State != State.COMPLETE)
					{
						Console.WriteLine("");
						// Console.WriteLine($"Player1: {game.Player1.PlayState} / Player2: {game.Player2.PlayState} - " +
						// 				  $"ROUND {(game.Turn + 1) / 2} - {game.CurrentPlayer.Name}");
						Console.WriteLine($"Hero[P1]: {game.Player1.Hero.Health} / Hero[P2]: {game.Player2.Hero.Health}");
						Console.WriteLine("");
						while (game.State == State.RUNNING && game.CurrentPlayer == game.Player1)
						{
							//Console.WriteLine($"* Calculating solutions *** Player 1 ***");
							List<OptionNode> solutions = OptionNode.GetSolutions(game, game.Player1.Id, aiPlayer1, 10, 500);
							var solution = new List<PlayerTask>();
							solutions.OrderByDescending(p => p.Score).First().PlayerTasks(ref solution);
							//Console.WriteLine($"- Player 1 - <{game.CurrentPlayer.Name}> ---------------------------");
							foreach (PlayerTask task in solution)
							{
								Console.WriteLine(task.FullPrint());
								game.Process(task);
								if (game.CurrentPlayer.Choice != null)
								{
									//Console.WriteLine($"* Recalculating due to a final solution ...");
									break;
								}
							}
						}

						// Random mode for Player 2
						Console.WriteLine("");
						//Console.WriteLine($"- Player 2 - <{game.CurrentPlayer.Name}> ---------------------------");
						while (game.State == State.RUNNING && game.CurrentPlayer == game.Player2)
						{
							//var options = game.Options(game.CurrentPlayer);
							//var option = options[Rnd.Next(options.Count)];
							//Log.Info($"[{option.FullPrint()}]");
							//game.Process(option);
							//Console.WriteLine($"* Calculating solutions *** Player 2 ***");
							List<OptionNode> solutions = OptionNode.GetSolutions(game, game.Player2.Id, aiPlayer2, 10, 500);
							var solution = new List<PlayerTask>();
							solutions.OrderByDescending(p => p.Score).First().PlayerTasks(ref solution);
							//Console.WriteLine($"- Player 2 - <{game.CurrentPlayer.Name}> ---------------------------");
							foreach (PlayerTask task in solution)
							{
								Console.WriteLine(task.FullPrint());
								game.Process(task);
								if (game.CurrentPlayer.Choice != null)
								{
									//Console.WriteLine($"* Recalculating due to a final solution ...");
									break;
								}
							}
						}
					}
					Console.WriteLine("");
					Console.WriteLine($"Game: {game.State}, Player1: {game.Player1.PlayState} / Player2: {game.Player2.PlayState}");

					if (game.Player1.PlayState == PlayState.WON)
						wins[0]++;
					if (game.Player2.PlayState == PlayState.WON)
						wins[1]++;
				}
				catch (Exception e){
					Console.WriteLine($"Exception caught: {e}");
					Console.WriteLine($"{e.ToString()}");
					Console.WriteLine("Composition of offending deck: ----------------------");
					foreach(Card card in deck){
						Console.WriteLine($"{card.ToString()}");
					}
					Console.WriteLine("Awarding win to opponent by technicality.");
					wins[1]++;
				}

				Console.WriteLine($"Player 1 (Population) Wins: {wins[0]} / Player 2 (Control) Wins: {wins[1]}");

			}
			watch.Stop();

			Console.WriteLine($"{total} games took {watch.ElapsedMilliseconds / 1000 / 60} minutes!");
			Console.WriteLine($"Player 1 (Population) {wins[0] * 100 / total}% vs. Player 2 (Control) {wins[1] * 100 / total}%!");
			return wins[0] * 100 / total;

		}


		public static void RandomGames()
		{
			int total = 1000;
			var watch = Stopwatch.StartNew();

			MemberDeck player1 = new MemberDeck();
			var gameConfig = new GameConfig()
			{
				StartPlayer = 1,
				Player1Name = "FitzVonGerald",
				Player1HeroClass = hero,
				Player1Deck = player1.deck,
				Player2Name = "RehHausZuckFuchs",
				Player2HeroClass = CardClass.WARRIOR,
				Player2Deck = Decks.AggroPirateWarrior,
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
