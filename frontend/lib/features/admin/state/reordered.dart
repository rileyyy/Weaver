/// [items] with the one at [oldIndex] moved so it ends up at [newIndex],
/// as `ReorderableListView.onReorderItem` reports a drag.
List<T> reordered<T>(List<T> items, int oldIndex, int newIndex) {
  final result = [...items];
  final moved = result.removeAt(oldIndex);
  result.insert(newIndex, moved);
  return result;
}
