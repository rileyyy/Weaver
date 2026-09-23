import 'package:flutter/material.dart';

class SearchAndActions extends StatelessWidget {
  const SearchAndActions({
    super.key,
    required this.controller,
    required this.onSearchChanged,
    required this.onOpenFilters,
    required this.onLogout,
  });

  final TextEditingController controller;
  final ValueChanged<String> onSearchChanged;
  final VoidCallback onOpenFilters;
  final VoidCallback onLogout;

  @override
  Widget build(BuildContext context) {
    final searchField = TextField(
      controller: controller,
      onChanged: onSearchChanged,
      decoration: const InputDecoration(
        isDense: true,
        prefixIcon: Icon(Icons.search),
        hintText: 'Search title, description, or tags',
        border: OutlineInputBorder(),
      ),
    );

    return Row(
      children: [
        // Expanded (rather than a fixed width) so this row fits whatever
        // space the header layout gives it — a fixed width overflowed on
        // narrower desktop/tablet widths where the header is still in its
        // wide, three-section row arrangement.
        Expanded(child: searchField),
        const SizedBox(width: 8),
        IconButton(
          icon: const Icon(Icons.filter_list),
          tooltip: 'Filters',
          onPressed: onOpenFilters,
        ),
        IconButton(
          icon: const Icon(Icons.logout),
          tooltip: 'Sign out',
          onPressed: onLogout,
        ),
      ],
    );
  }
}
