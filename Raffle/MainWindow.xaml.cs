using System;
using System.Linq;
using ReactiveUI;
using System.Collections.ObjectModel;
using System.Reactive.Linq;
using System.IO;
using System.Reactive;
using System.Collections.Generic;
using System.Text;

namespace Raffle
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        public MainWindow()
        {
            InitializeComponent();
            var viewModel = new ViewModel();
            DataContext = viewModel;
            MediaElement_Run.MediaEnded += (s, e) => MediaElement_Run.Position = TimeSpan.FromMilliseconds(1);
            viewModel.RunCommand.Subscribe(isRun =>
            {
                if(isRun)
                {
                    MediaElement_Run.Play();
                    return;
                }
                MediaElement_Run.Stop();
            });
        }
    }

    public sealed class ViewModel : ReactiveObject
    {
        public ViewModel()
        {
            string peoplePath = "People.txt";
            if (!File.Exists(peoplePath))
            {
                throw new Exception("不存在人员名单");
            }
            _people = new ObservableCollection<Person>(File.ReadAllLines(peoplePath).Select(name => new Person(name)));
            Upset();
            string awardsPath = "Awards.txt";
            if (!File.Exists(awardsPath))
            {
                throw new Exception("不存在奖项名单");
            }
            Awards = new ObservableCollection<Award>(File.ReadAllLines(awardsPath).Select(str => str.Split(' ')).Select(strs => new Award(strs[0], int.Parse(strs[1]), strs.Skip(2).Select(name =>
            {
                var person = _people.First(person => person.Name == name);
                person.IsChecked = true;
                _people.Remove(person);
                return person;
            }))));
            _selectedAwardIndex = 0;
            IDisposable? disposable = null;
            Person? old = null;
            _selectedAwardHelper = this.WhenAnyValue(t => t.SelectedAwardIndex, index => Awards[index]).ToProperty(this, nameof(SelectedAward), Awards[0]);
            RunCommand = ReactiveCommand.Create(() =>
            {
                if (disposable != null)
                {
                    disposable.Dispose();
                    disposable = null;
                    if (old == null)
                    {
                        throw new NotSupportedException();
                    }
                    SelectedAward.People.Add(old);
                    File.WriteAllLines(awardsPath, Awards.Select(award => award.ToString()));
                    return false;
                }
                if (old != null)
                {
                    _people.Remove(old);
                    old = null;
                    Upset();
                }
                Random random = new Random((int)DateTime.Now.ToFileTime());
                disposable = Observable.Timer(TimeSpan.FromMilliseconds(0), TimeSpan.FromMilliseconds(10), RxApp.TaskpoolScheduler).Select(l => random.Next(_people.Count - 1)).Subscribe(index =>
                {
                    if (old != null)
                    {
                        old.IsChecked = false;
                    }
                    old = _people[index];
                    old.IsChecked = true;
                });
                return true;
            }, this.WhenAnyValue(t => t.SelectedAward.People.Count).CombineLatest(this.WhenAnyValue(t => t.SelectedAward), (count, award) => count < award.Count));
            ClearCommand = ReactiveCommand.Create(() =>
            {
                foreach (var award in Awards)
                {
                    foreach (var person in award.People)
                    {
                        person.IsChecked = false;
                        _people.Add(person);
                    }
                    award.People.Clear();
                    Upset();
                }
                File.WriteAllLines(awardsPath, Awards.Select(award => award.ToString()));
            });
            DownCommand = ReactiveCommand.Create(() =>
            {
                SelectedAwardIndex = (_selectedAwardIndex + 1) % Awards.Count;
            }, this.WhenAnyValue(t => t.SelectedAwardIndex, index => index < Awards.Count - 1));
            UpCommand = ReactiveCommand.Create(() =>
            {
                SelectedAwardIndex = (_selectedAwardIndex - 1) % Awards.Count;
            }, this.WhenAnyValue(t => t.SelectedAwardIndex, index => index > 0));
        }

        private ObservableCollection<Person> _people;

        public ObservableCollection<Person> People
        {
            get
            {
                return _people;
            }

            private set
            {
                this.RaiseAndSetIfChanged(ref _people, value);
            }
        }

        public ObservableCollection<Award> Awards { get; }

        private int _selectedAwardIndex;

        public int SelectedAwardIndex
        {
            get
            {
                return _selectedAwardIndex;
            }

            set
            {
                this.RaiseAndSetIfChanged(ref _selectedAwardIndex, value);
            }
        }

        private readonly ObservableAsPropertyHelper<Award> _selectedAwardHelper;

        public Award SelectedAward => _selectedAwardHelper.Value;

        public string GifPath { get; } = "Run.gif";

        public ReactiveCommand<Unit, bool> RunCommand { get; }

        public ReactiveCommand<Unit, Unit> ClearCommand { get; }

        public ReactiveCommand<Unit, Unit> UpCommand { get; }

        public ReactiveCommand<Unit, Unit> DownCommand { get; }

        private void Upset()
        {
            People = new ObservableCollection<Person>(_people.OrderBy(person => Guid.NewGuid()));
        }
    }

    public sealed class Person : ReactiveObject
    {
        public Person(string name)
        {
            Name = name;
        }

        public string Name { get; }

        private bool _isChecked;

        public bool IsChecked
        {
            get
            {
                return _isChecked;
            }

            set
            {
                this.RaiseAndSetIfChanged(ref _isChecked, value);
            }
        }
    }

    public sealed class Award : ReactiveObject
    {
        public Award(string name, int count, IEnumerable<Person> people)
        {
            Name = name;
            Count = count;
            People = new ObservableCollection<Person>(people);
        }

        public string Name { get; }

        public int Count { get; }

        public ObservableCollection<Person> People { get; }

        public override string ToString()
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append(Name);
            stringBuilder.Append(' ');
            stringBuilder.Append(Count);
            foreach(var person in People)
            {
                stringBuilder.Append(' ');
                stringBuilder.Append(person.Name);
            }
            return stringBuilder.ToString();
        }
    }
}
