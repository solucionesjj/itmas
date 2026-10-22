import { AUDIT_ACTION_LABELS } from './authorization.models';

/**
 * The audit screen shows the actions in Spanish (SPEC-0001 §14.3); SPEC-0006 §12.2 adds the ones of the agenda. An action with
 * no label would appear with its technical name.
 */
describe('audit labels of the agenda (SPEC-0006 §12.2)', () => {
  const AGENDA_ACTIONS = [
    'agenda.event.created',
    'agenda.event.updated',
    'agenda.event.deleted',
    'agenda.event.scheduled',
    'agenda.event.cancelled',
    'agenda.event.completed',
    'agenda.event.completion-updated',
    'agenda.event.tags-updated',
    'agenda.series.created',
    'agenda.series.updated',
    'agenda.event-type.created',
    'agenda.event-type.updated',
    'agenda.event.outlook-published',
    'agenda.event.outlook-failed',
  ];

  it('has a Spanish label for every action of the agenda', () => {
    expect(AGENDA_ACTIONS.filter((action) => !AUDIT_ACTION_LABELS[action])).toEqual([]);
    for (const action of AGENDA_ACTIONS) {
      expect(AUDIT_ACTION_LABELS[action], action).not.toMatch(/agenda\.|[a-z]+\.[a-z]+/);
    }
  });

  it('has no label for an action that does not exist', () => {
    const labelled = Object.keys(AUDIT_ACTION_LABELS).filter((action) =>
      action.startsWith('agenda.'),
    );

    expect(labelled.sort()).toEqual([...AGENDA_ACTIONS].sort());
  });
});
