using System;
using System.Linq;
using ReactiveUI;
using System.Collections.ObjectModel;
using System.Reactive.Linq;
using System.IO;
using System.Reactive;
using System.Collections.Generic;
using System.Text;
using System.Windows.Controls;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows;

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
            _viewModel = new ViewModel();
            DataContext = _viewModel;
            MediaElement_Run.MediaEnded += (s, e) => MediaElement_Run.Position = TimeSpan.FromMilliseconds(1);
            //ListBox_Awards.SelectionChanged += (s, e) => ((ListBox)s).ScrollIntoView(e.AddedItems[0]);
            //Button_Run.Focus();
            //Button_Run.LostFocus += async (s, e) =>
            //{
            //    await Task.Delay(100);
            //    Button_Run.Focus();
            //};
        }

        private ViewModel _viewModel;

        private void StackPanel_Main_IsEnabledChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if ((bool)e.NewValue)
            {
                var stackPanel = (StackPanel)sender;
                var award = (Award)stackPanel.DataContext;
                var index = _viewModel.Awards.IndexOf(award);
                double offset = ScrollViewer_Awards.VerticalOffset;
                double newOffset = stackPanel.ActualHeight * index;
                ScrollViewer_Awards.ScrollToVerticalOffset(newOffset);
                //if (newOffset<=offset)
                //{
                //    ScrollViewer_Awards.ScrollToVerticalOffset(newOffset);
                //}
                //else
                //{
                //    ScrollViewer_Awards.ScrollToVerticalOffset(stackPanel.ActualHeight * (index + 1));
                //}
            }
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
            Person? old = null;
            string awardsPath = "Awards.txt";
            _addCommand = ReactiveCommand.Create<Person>(person =>
            {
                if (!person.IsChecked && SelectedAward.Count > SelectedAward.People.Count)
                {
                    person.Command = _deleteCommand;
                    SelectedAward.People.Add(person);
                    _people.Remove(person);
                    person.IsChecked = true;
                    Save();
                }
            });
            _deleteCommand = ReactiveCommand.Create<Person>(person =>
            {
                person.Command = _addCommand;
                foreach(var award in Awards)
                {
                    if(award.People.Contains(person))
                    {
                        award.People.Remove(person);
                        break;
                    }
                }
                if (old != person)
                {
                    _people.Add(person);
                }
                person.IsChecked = false;
                Save();
            });
            _people = new ObservableCollection<Person>(File.ReadAllLines(peoplePath).Select(name => new Person(name, _addCommand)));
            if (!File.Exists(awardsPath))
            {
                throw new Exception("不存在奖项名单");
            }
            Awards = new ObservableCollection<Award>(File.ReadAllLines(awardsPath).Select(str => str.Split(' ')).Select(strs => new Award(strs[0], int.Parse(strs[1]), strs.Skip(2).Select(name =>
            {
                var person = _people.First(person => person.Name == name);
                person.Command = _deleteCommand;
                person.IsChecked = true;
                _people.Remove(person);
                return person;
            }))));
            _selectedAwardIndex = 0;
            IDisposable? disposable = null;
            _selectedAwardHelper = this.WhenAnyValue(t => t.SelectedAwardIndex, index => Awards[index]).ToProperty(this, nameof(SelectedAward), Awards[0]);
            this.WhenAnyValue(t => t.SelectedAward).Subscribe(award =>
            {
                if(_selectedAward !=null)
                {
                    _selectedAward.IsChecked = false;
                }
                award.IsChecked = true;
                _selectedAward = award;
            });
            RunCommand = ReactiveCommand.CreateFromTask(async () =>
            {
                Random random = new Random((int)DateTime.Now.ToFileTime());
                if (disposable != null)
                {
                    await Task.Delay(random.Next(300, 1000));
                    disposable.Dispose();
                    disposable = null;
                    if (old == null)
                    {
                        throw new NotSupportedException();
                    }
                    old.Command = _deleteCommand;
                    SelectedAward.People.Add(old);
                    Save();
                    return false;
                }
                if (old != null)
                {
                    _people.Remove(old);
                    old = null;
                }
                People = new ObservableCollection<Person>(_people.OrderBy(person => random.Next(_people.Count - 1)));
                disposable = Observable.Timer(TimeSpan.FromMilliseconds(0), TimeSpan.FromMilliseconds(100), RxApp.TaskpoolScheduler).Select(l => random.Next(_people.Count - 1)).ObserveOn(RxApp.MainThreadScheduler).Subscribe(index =>
                {
                    if (old != null)
                    {
                        old.IsChecked = false;
                    }
                    old = _people[index];
                    old.IsChecked = true;
                });
                return true;
            }, this.WhenAnyValue(t => t.SelectedAward.IsFilled, isFilled => !isFilled));
            _isRunHelper = RunCommand.ToProperty(this, nameof(IsRun));
            _gifBehaviorHelper = RunCommand.Select(b => b ? MediaState.Play : MediaState.Stop).ToProperty(this, nameof(GifBehavior));
            ClearCommand = ReactiveCommand.Create(() =>
            {
                foreach (var award in Awards)
                {
                    foreach (var person in award.People)
                    {
                        person.Command = _addCommand;
                        person.IsChecked = false;
                        if (old != person)
                        {
                            _people.Add(person);
                        }
                    }
                    award.People.Clear();
                }
                Save();
            });
            DownCommand = ReactiveCommand.Create(() =>
            {
                SelectedAwardIndex = (_selectedAwardIndex + 1) % Awards.Count;
            }, this.WhenAnyValue(t => t.SelectedAwardIndex, index => index < Awards.Count - 1));
            UpCommand = ReactiveCommand.Create(() =>
            {
                SelectedAwardIndex = (_selectedAwardIndex - 1) % Awards.Count;
            }, this.WhenAnyValue(t => t.SelectedAwardIndex, index => index > 0));

            void Save()
            {
                File.WriteAllLines(awardsPath, Awards.Select(award => award.ToString()));
            }
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

        private Award? _selectedAward;

        private readonly ObservableAsPropertyHelper<Award> _selectedAwardHelper;

        public Award SelectedAward => _selectedAwardHelper.Value;

        private readonly ObservableAsPropertyHelper<bool> _isRunHelper;

        public bool IsRun => _isRunHelper.Value;

        public string GifPath { get; } = "Run.gif";

        private readonly ObservableAsPropertyHelper<MediaState> _gifBehaviorHelper;

        public MediaState GifBehavior => _gifBehaviorHelper.Value;

        public ReactiveCommand<Unit, bool> RunCommand { get; }

        public ReactiveCommand<Unit, Unit> ClearCommand { get; }

        public ReactiveCommand<Unit, Unit> UpCommand { get; }

        public ReactiveCommand<Unit, Unit> DownCommand { get; }

        private readonly ReactiveCommand<Person, Unit> _addCommand;

        private readonly ReactiveCommand<Person, Unit> _deleteCommand;
    }

    public sealed class Person : ReactiveObject
    {
        public Person(string name, ICommand command)
        {
            Name = name;
            Command = command;
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

        public ICommand Command { get; set; }
    }

    public sealed class Award : ReactiveObject
    {
        public Award(string name, int count, IEnumerable<Person> people)
        {
            Name = name;
            Count = count;
            People = new ObservableCollection<Person>(people);
            _isFilledHelper = this.WhenAnyValue(t => t.People.Count, count => count >= Count).ToProperty(this, nameof(IsFilled));
        }

        public string Name { get; }

        public int Count { get; }

        public ObservableCollection<Person> People { get; }

        private readonly ObservableAsPropertyHelper<bool> _isFilledHelper;

        public bool IsFilled => _isFilledHelper.Value;

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
