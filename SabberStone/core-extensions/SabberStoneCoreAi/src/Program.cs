using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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

		static int populationSize = 10; // How many decks are in a single generation.
		static int generationLimit = 100; // How many generations to run in a single sitting.
		static int games = 10; // How many games to play per fitness evaluation

		static int poolSize = 5;
		static double mutationChance = 0.10d; // The probability for a random new child deck to mutate.

		static List<Card> allCards;
		static List<Card> availableCards; // List of cards to choose from when creating / mutating a deck.
		static CardClass hero = CardClass.PALADIN; // The class used for all population decks.

		static List<MemberDeck> population; // set of all the members of the current generation.
		static List<MemberDeck> pool; // set of the most fit members of the current generation.

		static ParallelOptions options = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }; // for multithreading

		public class MemberDeck
		{
			public List<Card> deck;
			public double fitness; // Represents how fit the deck is, usually by winrate.

			public MemberDeck(){
				deck = randomCards(30); // for inital population members
				fitness = 0d;
			}

			public MemberDeck(List<Card> assignedDeck){
				deck = assignedDeck; // for child population members
				fitness = 0d;
			}

			public void mutateDeck(){
				// if (random < mutationChance), mutate a random card in the deck.
				if (Rnd.NextDouble() < mutationChance){
					// Choose a card in the original deck to mutate.
					int oldCard = Rnd.Next(deck.Count);
					// Choose a new card to replace it with.
					Card newCard = randomCards(1)[0];

					// generate a new card until it's valid for this deck.
					int count = deck.Where(s=>s!=null && s.Equals(newCard)).Count();
					while (!newCard.Equals(deck[oldCard]) && ((newCard.Rarity == Rarity.LEGENDARY && count > 0) || (newCard.Rarity != Rarity.LEGENDARY && count > 1))) {
						newCard = randomCards(1)[0];
						count = deck.Where(s=>s!=null && s.Equals(newCard)).Count();
					}
					deck[oldCard] = newCard;
				}
			}

		}

		// naive implementation: randomly take half the cards from one parent and half the cards from the other to form a child.
		public static List<Card> childDeck(List<Card> first_parent, List<Card> second_parent)
		{
			List<Card> child = new List<Card>();

			for (int i = 0; i < 15; i++){
				int r = Rnd.Next(first_parent.Count);
				Card newCard = first_parent[r];
				int count = child.Where(s=>s!=null && s.Equals(newCard)).Count();

				// generate a new card until it's valid for this deck.
				while ((newCard.Rarity == Rarity.LEGENDARY && count > 0) || (newCard.Rarity != Rarity.LEGENDARY && count > 1)) {
					r = Rnd.Next(first_parent.Count);
					newCard = first_parent[r];
					count = child.Where(s=>s!=null && s.Equals(newCard)).Count();
				}
				child.Add(newCard);
			}

			for (int i = 0; i < 15; i++){
				int r = Rnd.Next(second_parent.Count);
				Card newCard = second_parent[r];
				int count = child.Where(s=>s!=null && s.Equals(newCard)).Count();

				// generate a new card until it's valid for this deck.
				while ((newCard.Rarity == Rarity.LEGENDARY && count > 0) || (newCard.Rarity != Rarity.LEGENDARY && count > 1)) {
					r = Rnd.Next(second_parent.Count);
					newCard = second_parent[r];
					count = child.Where(s=>s!=null && s.Equals(newCard)).Count();
				}
				child.Add(newCard);
			}
			return child;
		}

		// Filter cards into the list of available cards
		public static void filterCards()
		{
			allCards = new List<Card>();
			foreach (Card card in Cards.AllStandard)
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
					allCards.Add(card);
			}
			availableCards = new List<Card>();
			foreach (Card card in allCards)
			{
				if (card.Class == CardClass.NEUTRAL || card.Class ==  hero)
					availableCards.Add(card);
			}
		}

		private static void Main(string[] args)
		{
			Console.WriteLine("Starting test setup.");
			// initialize available cards and generate initial population
			filterCards();
			population = new List<MemberDeck>();
			pool = new List<MemberDeck>();
			for (int k = 0; k < populationSize; k++){
				MemberDeck deck = new MemberDeck();
				population.Add(deck);
			}

			// running the generations
			int currentGeneration = 1;
			while (currentGeneration <= generationLimit){
				Console.WriteLine($"Current generation: {currentGeneration}");

				foreach (MemberDeck member in population){
					member.fitness = EvaluateFitness(member.deck);
				}

				for (int i = 0; i < populationSize; i++){	
					if (pool.Count == 0)
						pool.Add(population[i]);
					else if (population[i].fitness > pool[pool.Count - 1].fitness){
						int j = pool.Count - 1;
						for (; j >= 0; j--){
							if (j == 0 || population[i].fitness < pool[j-1].fitness)
								break;
						}
						pool.Insert(j, population[i]);
						// Trim the size of pool if its max is exceeded.
						if (pool.Count > poolSize)
							pool.RemoveAt(poolSize);
					}
					else if (pool.Count < poolSize)
						pool.Add(population[i]);
				}
				Console.WriteLine("");	
				Console.WriteLine($"Best fitness: {pool[0].fitness}");
				Console.WriteLine("Composition of best deck: ----------------------");
				for (int i = 0; i < 30; i++){
					Console.WriteLine($"{pool[0].deck[i].ToString()}");
				}
				// naive implementation of parent selection, direct random pool.
				// Always copy the most fit member of the previous generation.
				currentGeneration++;
				if (currentGeneration > generationLimit)
					break;
				else{
					population.Clear();
					population.Add(pool[0]);
					for (int k = 1; k < populationSize; k++){
						int first = Rnd.Next(pool.Count);
						int second = Rnd.Next(pool.Count);

						List<Card> child = childDeck(pool[first].deck, pool[second].deck);
						MemberDeck deck = new MemberDeck(child);
						deck.mutateDeck();

						population.Add(deck);
					}
					pool.Clear();
				}
			}	
			Console.WriteLine("Test end!");
		}

		public static List<Card> randomCards(int n)
		{
			List<Card> cards = new List<Card>();
			for (int i = 0; i < n; i++){
				int r = Rnd.Next(availableCards.Count);
				Card card = availableCards[r];
				int count = cards.Where(s=>s!=null && s.Equals(card)).Count();
				// check that the set of cards you're returning is valid as a standalone set.
				while ((card.Rarity == Rarity.LEGENDARY && count > 0) || (card.Rarity != Rarity.LEGENDARY && count > 1)) {
					r = Rnd.Next(availableCards.Count);
					card = availableCards[r];
					count = cards.Where(s=>s!=null && s.Equals(card)).Count();
				}
				cards.Add(card);
			}
			return cards;
		}

		public static double EvaluateFitness(List<Card> deck)
		{
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

			int player_wins = 0;
			int control_wins = 0;
			Parallel.For(0, games, options, i =>
			{
				var game = new Game(gameConfig);
				game.StartGame();

				var aiPlayer1 = new ControlScore();
				var aiPlayer2 = new AggroScore();

				List<int> mulligan1 = aiPlayer1.MulliganRule().Invoke(game.Player1.Choice.Choices.Select(p => game.IdEntityDic[p]).ToList());
				List<int> mulligan2 = aiPlayer2.MulliganRule().Invoke(game.Player2.Choice.Choices.Select(p => game.IdEntityDic[p]).ToList());

				game.Process(ChooseTask.Mulligan(game.Player1, mulligan1));
				game.Process(ChooseTask.Mulligan(game.Player2, mulligan2));

				game.MainReady();
				try
				{
					while (game.State != State.COMPLETE)
					{
						//Console.WriteLine($"Hero[P1]: {game.Player1.Hero.Health} / Hero[P2]: {game.Player2.Hero.Health}");
						while (game.State == State.RUNNING && game.CurrentPlayer == game.Player1)
						{
							List<OptionNode> solutions = OptionNode.GetSolutions(game, game.Player1.Id, aiPlayer1, 10, 500);
							var solution = new List<PlayerTask>();
							solutions.OrderByDescending(p => p.Score).First().PlayerTasks(ref solution);
							foreach (PlayerTask task in solution)
							{
								//Console.WriteLine(task.FullPrint());
								game.Process(task);
								if (game.CurrentPlayer.Choice != null)
								{
									break;
								}
							}
						}
						while (game.State == State.RUNNING && game.CurrentPlayer == game.Player2)
						{;
							List<OptionNode> solutions = OptionNode.GetSolutions(game, game.Player2.Id, aiPlayer2, 10, 500);
							var solution = new List<PlayerTask>();
							solutions.OrderByDescending(p => p.Score).First().PlayerTasks(ref solution);
							foreach (PlayerTask task in solution)
							{
								//Console.WriteLine(task.FullPrint());
								game.Process(task);
								if (game.CurrentPlayer.Choice != null)
									break;
							}
						}
					}
					if (game.Player1.PlayState == PlayState.WON)
						Interlocked.Increment(ref player_wins);
					else if (game.Player2.PlayState == PlayState.WON)
						Interlocked.Increment(ref control_wins);
				}
				catch (Exception e){
					Console.WriteLine($"Exception caught: {e}");
					Console.WriteLine("Composition of offending deck:");
					foreach(Card card in deck){
						Console.WriteLine($"{card.ToString()}");
					}
					Console.WriteLine("Awarding win to opponent.");
					Interlocked.Increment(ref control_wins);
				}
				Console.WriteLine($"Player 1 (Population) Wins: {player_wins} / Player 2 (Control) Wins: {control_wins}");
			});
			watch.Stop();
			Console.WriteLine("");
			Console.WriteLine($"{games} games took {watch.ElapsedMilliseconds / 1000 / 60} minutes!");
			Console.WriteLine($"Player 1 (Population) {player_wins * 100 / games}% vs. Player 2 (Control) {control_wins * 100 / games}%!");
			Console.WriteLine("");
			return player_wins * 100 / games;
		}
	}
}
