/// Display order for cards within a status column. This is a client-side
/// view concern only — it never touches the backend `Rank` that drag-and-drop
/// relies on, so switching away from [manual] and back leaves the underlying
/// order untouched.
enum CardSortOption { manual, title, startDate, dueDate }

extension CardSortOptionLabel on CardSortOption {
  String get label => switch (this) {
    CardSortOption.manual => 'Manual',
    CardSortOption.title => 'Title (A–Z)',
    CardSortOption.startDate => 'Start date',
    CardSortOption.dueDate => 'Due date',
  };
}
