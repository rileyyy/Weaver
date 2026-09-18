import 'package:injectable/injectable.dart';
import 'package:weaver/core/presentation/view_model.dart';

@injectable
class HomeViewModel extends ViewModel {
  String get title => 'Weaver';

  String get message =>
      'This is the app shell. The board arrives in a later milestone.';
}
