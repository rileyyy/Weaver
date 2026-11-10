import 'package:injectable/injectable.dart';
import 'package:weaver/features/board/data/backlog_repository.dart';
import 'package:weaver/features/board/data/board_repository.dart';

@module
abstract class BacklogRepositoryModule {
  /// The board's repository already does everything the Backlog needs; the
  /// Backlog just sees no more of it than that.
  @lazySingleton
  BacklogRepository backlogRepository(BoardRepository board) => board;
}
