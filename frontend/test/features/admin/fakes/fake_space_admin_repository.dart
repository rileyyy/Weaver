import 'package:weaver/core/network/api_exception.dart';
import 'package:weaver/features/admin/data/space_admin_repository.dart';
import 'package:weaver/features/admin/models/space_history_entry.dart';
import 'package:weaver/shared/models/space.dart';

class FakeSpaceAdminRepository implements SpaceAdminRepository {
  FakeSpaceAdminRepository(
    List<Space> spaces, {
    Map<String, List<SpaceHistoryEntry>> history = const {},
  }) : spaces = [...spaces],
       _history = history;

  final List<Space> spaces;
  final Map<String, List<SpaceHistoryEntry>> _history;
  ApiException? error;
  final List<String> calls = [];

  @override
  Future<List<Space>> list() async {
    calls.add('list');
    _throwIfFailing();
    return [...spaces];
  }

  @override
  Future<Space> create(String name) async {
    calls.add('create $name');
    _throwIfFailing();
    final space = Space(id: 'space-${spaces.length + 1}', name: name);
    spaces.add(space);
    return space;
  }

  @override
  Future<Space> rename(String id, String name) async {
    calls.add('rename $id $name');
    _throwIfFailing();
    final index = spaces.indexWhere((s) => s.id == id);
    final renamed = Space(
      id: id,
      name: name,
      doneHidingDays: spaces[index].doneHidingDays,
    );
    spaces[index] = renamed;
    return renamed;
  }

  @override
  Future<Space> setDoneHidingPeriod(String id, int days) async {
    calls.add('setDoneHidingPeriod $id $days');
    _throwIfFailing();
    final index = spaces.indexWhere((s) => s.id == id);
    final changed = Space(
      id: id,
      name: spaces[index].name,
      doneHidingDays: days,
    );
    spaces[index] = changed;
    return changed;
  }

  @override
  Future<List<SpaceHistoryEntry>> history(String id) async {
    calls.add('history $id');
    _throwIfFailing();
    return [...?_history[id]];
  }

  void _throwIfFailing() {
    final error = this.error;
    if (error != null) throw error;
  }
}
