import 'package:weaver/core/network/api_exception.dart';
import 'package:weaver/features/admin/data/space_admin_repository.dart';
import 'package:weaver/shared/models/space.dart';

class FakeSpaceAdminRepository implements SpaceAdminRepository {
  FakeSpaceAdminRepository(List<Space> spaces) : spaces = [...spaces];

  final List<Space> spaces;
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

  void _throwIfFailing() {
    final error = this.error;
    if (error != null) throw error;
  }
}
