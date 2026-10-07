import { Permission } from './authorization.models';
import { groupByCategory } from './permission-groups';

const permission = (name: string, category: string): Permission => ({
  name,
  category,
  displayName: name,
  description: '',
  isGrantable: true,
  isDeprecated: false,
  policyCount: 0,
});

describe('groupByCategory', () => {
  it('groups by category in alphabetical order, keeping the catalog order inside each group', () => {
    const groups = groupByCategory([
      permission('users.read', 'Autorización'),
      permission('reports.read', 'Reportes'),
      permission('users.update', 'Autorización'),
    ]);

    expect(groups.map((group) => group.category)).toEqual(['Autorización', 'Reportes']);
    expect(groups[0].permissions.map((item) => item.name)).toEqual(['users.read', 'users.update']);
  });
});
