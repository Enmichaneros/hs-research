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

		static CardClass hero = CardClass.PALADIN; // The class used for all population decks.

		static int populationSize = 20; // How many decks are in a single generation.
		static int generationLimit = 10; // How many generations to run in a single sitting.
		static int games = 10; // How many games to play per fitness evaluation

		static int poolSize = 10; // Top N decks to use as potential parents for the next generation.
		static double mutationChance = 0.10d; // The probability for a random new child deck to mutate.

		static List<Card> availableCards; // List of cards to choose from when creating / mutating a deck.
		static ParallelOptions options = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }; // for multithreading

		// representation of a population member
		public class MemberDeck
		{
			public List<Card> deck;
			public double fitness; // Represents how fit the deck is, determined by winrate.

			public MemberDeck(){
				deck = randomCards(30); // for inital population members
				fitness = 0d;
			}

			public MemberDeck(List<Card> assignedDeck){
				deck = assignedDeck; // for child population members
				fitness = 0d;
			}

			public void mutateDeck(){
				// if (random < mutationChance), replace a random card in the deck.
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

		// Filter cards into the list of available cards
		public static void filterCards()
		{
			availableCards = new List<Card>();
			foreach (Card card in Cards.AllStandard)
			{
				if (card.Class == CardClass.NEUTRAL || card.Class ==  hero)
					availableCards.Add(card);
			}
		}

		// generate N random cards from the entire deckspace of legal cards
		public static List<Card> randomCards(int n)
		{
			List<Card> cards = new List<Card>();
			for (int i = 0; i < n; i++){
				int r = Rnd.Next(availableCards.Count);
				Card card = availableCards[r];
				int count = cards.Where(s=>s!=null && s.Equals(card)).Count();
				// check that you're not exceeding valid card counts
				while ((card.Rarity == Rarity.LEGENDARY && count > 0) || (card.Rarity != Rarity.LEGENDARY && count > 1)) {
					r = Rnd.Next(availableCards.Count);
					card = availableCards[r];
					count = cards.Where(s=>s!=null && s.Equals(card)).Count();
				}
				cards.Add(card);
			}
			return cards;
		}

		// naive implementation: randomly take half the cards from one parent and half the cards from the other to form a child.
		public static List<Card> childDeck(List<Card> first_parent, List<Card> second_parent)
		{
			List<Card> child = new List<Card>();

			for (int i = 0; i < 15; i++){
				int r = Rnd.Next(first_parent.Count);
				Card newCard = first_parent[r];
				int count = child.Where(s=>s!=null && s.Equals(newCard)).Count();

				// check that you're not exceeding valid card counts
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

				// check that you're not exceeding valid card counts
				while ((newCard.Rarity == Rarity.LEGENDARY && count > 0) || (newCard.Rarity != Rarity.LEGENDARY && count > 1)) {
					r = Rnd.Next(second_parent.Count);
					newCard = second_parent[r];
					count = child.Where(s=>s!=null && s.Equals(newCard)).Count();
				}
				child.Add(newCard);
			}
			return child;
		}

		// determine the "fitness" of the deck by its performance against the control deck
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
			// simulate games between the member deck and the control deck, tracking wins
			// most efficient performance seems to be 1 game / thread per core
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
									break;
							}
						}
						while (game.State == State.RUNNING && game.CurrentPlayer == game.Player2)
						{
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
				Console.WriteLine($"Player 1 Wins: {player_wins} / Player 2 Wins: {control_wins}");
			});
			watch.Stop();
			Console.WriteLine("");
			Console.WriteLine($"{games} games took {watch.ElapsedMilliseconds / 1000 / 60} minutes!");
			Console.WriteLine($"Player 1 (Population) {player_wins * 100 / games}% vs. Player 2 (Control) {control_wins * 100 / games}%!");
			Console.WriteLine("");
			return player_wins * 100 / games;
		}

		private static void Main(string[] args)
		{
			Console.WriteLine("Starting test setup.");
			Console.WriteLine("");	
			// initialize available cards and generate initial population
			filterCards();
			List<MemberDeck> population = new List<MemberDeck>();
			List<MemberDeck> pool = new List<MemberDeck>();

			for (int k = 0; k < populationSize; k++){
				MemberDeck deck = new MemberDeck();
				population.Add(deck);
			}

			// running the generations
			int currentGeneration = 1;
			while (currentGeneration <= generationLimit){
				Console.WriteLine($"Current generation: {currentGeneration}");
				Console.WriteLine("---------------------------------------");	
				// evaluate fitness of each deck
				int count = 1;
				foreach (MemberDeck member in population){
					Console.WriteLine($"Evaluating deck #{count++}:");
					member.fitness = EvaluateFitness(member.deck);
					// member.fitness = Rnd.NextDouble();
				}
				// select the top N decks as the breeding pool.
				// slightly more efficient than sorting the entire list
				double average = 0d; 
				for (int i = 0; i < populationSize; i++){
					average += population[i].fitness;	
					if (pool.Count > 0 && population[i].fitness > pool[pool.Count - 1].fitness){
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
				Console.WriteLine($"Best fitness: {pool[0].fitness} || Average fitness: {average / populationSize}");
				Console.WriteLine("Composition of best deck:");
				for (int i = 0; i < 30; i++){
					Console.WriteLine($"{pool[0].deck[i].ToString()}");
				}
				Console.WriteLine("");
				currentGeneration++;
				if (currentGeneration > generationLimit)
					break;
				// naive implementation of parent selection, direct random pool.
				else{
					population.Clear();
					population.Add(pool[0]); // Always copy the most fit member of the previous generation.
					for (int k = 1; k < populationSize; k++){
						int first = Rnd.Next(pool.Count);
						int second = Rnd.Next(pool.Count);
						// create a child from two parents, then give it the chance to mutate.
						List<Card> child = childDeck(pool[first].deck, pool[second].deck);
						MemberDeck deck = new MemberDeck(child);
						deck.mutateDeck();

						population.Add(deck);
					}
					pool.Clear();
				}
			} // end while	
			Console.WriteLine("Test end!");
		}
	}
}
