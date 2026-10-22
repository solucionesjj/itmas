import { Component, DestroyRef, OnInit, inject, input, output, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ReactiveFormsModule } from '@angular/forms';
import { MatAutocompleteModule } from '@angular/material/autocomplete';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { Subject, catchError, distinctUntilChanged, map, of, switchMap, timer } from 'rxjs';
import { ChipField } from '../components/chip-field';
import { TagsField } from '../components/tags-field';
import { AgendaApi } from '../data/agenda-api';
import { normalizeEmail } from '../data/agenda-fields';
import { IMPACTS, durationLabel, impactLabel, reminderLabel } from '../data/agenda-labels';
import { EventType, LIMITS, PersonOption } from '../data/agenda.models';
import { EventForm, REMINDER_PRESETS, personName } from './event-form';

/**
 * The fields of an activity (SPEC-0006 RF-3, §11), shared by the activity page and the series page. The form lives in the
 * page; this component only draws it. A series edit shows the common fields only (RF-6): no dates and no notice option.
 */
@Component({
  selector: 'app-event-fields',
  imports: [
    ChipField,
    MatAutocompleteModule,
    MatDatepickerModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatSlideToggleModule,
    ReactiveFormsModule,
    TagsField,
  ],
  template: `
    @let selected = selectedType();
    <form [formGroup]="form()" class="fields" (ngSubmit)="submitted.emit()">
      <mat-form-field appearance="outline" subscriptSizing="dynamic" class="wide">
        <mat-label>Tipo de actividad</mat-label>
        <mat-select formControlName="typeId" required>
          @for (type of types(); track type.id) {
            <mat-option [value]="type.id">
              {{ type.name }}{{ type.isActive ? '' : ' (inactivo)' }}
            </mat-option>
          }
        </mat-select>
        @if (selected) {
          <mat-hint>
            Por defecto: {{ durationText(selected) }}, recordatorio {{ reminderText(selected) }}
            @if (selected.isFreeze) {
              · Periodo de congelamiento: no deben programarse cambios.
            }
          </mat-hint>
        }
        @if (form().controls.typeId.hasError('required')) {
          <mat-error>El tipo es obligatorio.</mat-error>
        }
      </mat-form-field>

      <mat-form-field appearance="outline" subscriptSizing="dynamic" class="wide">
        <mat-label>Título</mat-label>
        <input matInput formControlName="title" [attr.maxlength]="limits.title" required />
        <mat-hint align="end">{{ form().controls.title.value.length }}/{{ limits.title }}</mat-hint>
        @if (form().controls.title.hasError('required')) {
          <mat-error>El título es obligatorio.</mat-error>
        }
      </mat-form-field>

      <mat-form-field appearance="outline" subscriptSizing="dynamic" class="wide">
        <mat-label>Descripción (opcional)</mat-label>
        <textarea
          matInput
          formControlName="description"
          rows="3"
          [attr.maxlength]="limits.description"
        ></textarea>
        <mat-hint>
          No escribas contraseñas, claves ni hallazgos de seguridad: el calendario llega a Outlook.
        </mat-hint>
      </mat-form-field>

      @if (!commonOnly()) {
        <mat-slide-toggle formControlName="isAllDay" class="wide">Todo el día</mat-slide-toggle>

        <div class="row wide">
          <mat-form-field appearance="outline" subscriptSizing="dynamic">
            <mat-label>{{ allDay() ? 'Primer día' : 'Fecha de inicio' }}</mat-label>
            <input matInput [matDatepicker]="startPicker" formControlName="startDate" required />
            <mat-datepicker-toggle matIconSuffix [for]="startPicker" />
            <mat-datepicker #startPicker />
            @if (form().controls.startDate.hasError('required')) {
              <mat-error>La fecha de inicio es obligatoria.</mat-error>
            }
          </mat-form-field>
          @if (!allDay()) {
            <mat-form-field appearance="outline" subscriptSizing="dynamic">
              <mat-label>Hora de inicio</mat-label>
              <input matInput type="time" formControlName="startTime" />
            </mat-form-field>
          }
          <mat-form-field appearance="outline" subscriptSizing="dynamic">
            <mat-label>{{ allDay() ? 'Último día (inclusive)' : 'Fecha de fin' }}</mat-label>
            <input matInput [matDatepicker]="endPicker" formControlName="endDate" />
            <mat-datepicker-toggle matIconSuffix [for]="endPicker" />
            <mat-datepicker #endPicker />
            <mat-hint>
              {{
                allDay()
                  ? 'Si la dejas vacía, dura un día.'
                  : 'Si no indicas el fin, se usa la duración por defecto del tipo.'
              }}
            </mat-hint>
          </mat-form-field>
          @if (!allDay()) {
            <mat-form-field appearance="outline" subscriptSizing="dynamic">
              <mat-label>Hora de fin</mat-label>
              <input matInput type="time" formControlName="endTime" />
            </mat-form-field>
          }
        </div>
        @if (timingError(); as message) {
          <p class="error wide" role="alert">{{ message }}</p>
        }
        <p class="hint wide">Las horas son las de la zona horaria de la organización.</p>
      }

      <mat-form-field appearance="outline" subscriptSizing="dynamic" class="wide">
        <mat-label>Responsable</mat-label>
        <input
          matInput
          formControlName="responsible"
          [matAutocomplete]="personAuto"
          (input)="typedPerson.next($any($event.target).value)"
          required
        />
        <mat-autocomplete #personAuto="matAutocomplete" [displayWith]="personName">
          @for (person of people(); track person.id) {
            <mat-option [value]="person">
              {{ person.name }}{{ person.isMe ? ' (tú)' : '' }}
            </mat-option>
          }
        </mat-autocomplete>
        <mat-hint>Elige una persona activa de ITMas.</mat-hint>
        @if (form().controls.responsible.hasError('required')) {
          <mat-error>El responsable es obligatorio.</mat-error>
        } @else if (form().controls.responsible.hasError('notAPerson')) {
          <mat-error>Elige una persona de la lista.</mat-error>
        }
      </mat-form-field>

      <app-chip-field
        class="wide"
        label="Participantes (opcional)"
        itemLabel="participante"
        placeholder="correo@dominio"
        hint="Reciben la invitación de Outlook. Escribe un correo y pulsa Intro."
        [max]="limits.participants"
        [maxMessage]="'Máximo ' + limits.participants + ' participantes por actividad.'"
        [normalize]="normalizeEmail"
        [values]="form().controls.participants.value"
        (valuesChange)="form().controls.participants.setValue($event)"
      />

      @if (!commonOnly()) {
        <mat-slide-toggle formControlName="notifyTeam" class="wide">
          Notificar al equipo
        </mat-slide-toggle>
      }

      <mat-form-field appearance="outline" subscriptSizing="dynamic">
        <mat-label>Impacto</mat-label>
        <mat-select formControlName="impact">
          @for (impact of impacts; track impact) {
            <mat-option [value]="impact">{{ impactText(impact) }}</mat-option>
          }
        </mat-select>
      </mat-form-field>

      <mat-form-field appearance="outline" subscriptSizing="dynamic">
        <mat-label>Recordatorio</mat-label>
        <mat-select formControlName="reminderMinutes">
          <mat-option [value]="null">
            Por defecto del tipo{{ selected ? ' (' + reminderText(selected) + ')' : '' }}
          </mat-option>
          @for (option of reminderOptions(); track option.minutes) {
            <mat-option [value]="option.minutes">{{ option.label }}</mat-option>
          }
        </mat-select>
      </mat-form-field>

      <mat-form-field appearance="outline" subscriptSizing="dynamic" class="wide">
        <mat-label>Alcance (opcional)</mat-label>
        <input
          matInput
          formControlName="scope"
          [attr.maxlength]="limits.scope"
          placeholder="Qué sistemas o equipos abarca"
        />
      </mat-form-field>

      <mat-form-field appearance="outline" subscriptSizing="dynamic" class="wide">
        <mat-label>Lugar (opcional)</mat-label>
        <input
          matInput
          formControlName="location"
          [attr.maxlength]="limits.location"
          placeholder="Por ejemplo, Remoto"
        />
      </mat-form-field>

      <app-tags-field
        class="wide"
        [tags]="form().controls.tags.value"
        (tagsChange)="form().controls.tags.setValue($event)"
      />
    </form>
  `,
  styles: `
    :host {
      display: block;
    }
    .fields {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(240px, 1fr));
      // The hints and errors of a field take the room they need (subscriptSizing="dynamic"): the gap keeps the fields apart.
      gap: 16px;
      align-items: start;
    }
    .wide {
      grid-column: 1 / -1;
    }
    .row {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
      gap: 16px;
    }
    .error {
      color: var(--mat-sys-error);
      font: var(--mat-sys-body-medium);
      margin: 0;
    }
    .hint {
      color: var(--mat-sys-on-surface-variant);
      font: var(--mat-sys-body-small);
      margin: 0;
    }
    mat-slide-toggle {
      margin: 8px 0;
    }
  `,
})
export class EventFields implements OnInit {
  private readonly api = inject(AgendaApi);

  readonly form = input.required<EventForm>();
  /** Types to choose from: the active ones, plus the current one of an activity whose type was deactivated. */
  readonly types = input<readonly EventType[]>([]);
  /** A series edit: dates and "notify the team" are not part of it (RF-6). */
  readonly commonOnly = input(false);
  /** On a new activity the person that is creating it is the suggested responsible. */
  readonly suggestMe = input(false);
  /** The form was submitted with Enter from a text field: the page saves, as with its button. */
  readonly submitted = output<void>();

  protected readonly limits = LIMITS;
  protected readonly impacts = IMPACTS;
  protected readonly impactText = impactLabel;
  protected readonly personName = personName;
  protected readonly normalizeEmail = normalizeEmail;
  protected readonly typedPerson = new Subject<string>();
  protected readonly people = signal<readonly PersonOption[]>([]);
  private suggested = false;

  constructor() {
    this.typedPerson
      .pipe(
        distinctUntilChanged(),
        // The people of an empty search (the suggestion) appear at once; what is typed waits for the person to pause.
        switchMap((text) => (text.trim() === '' ? of(text) : timer(250).pipe(map(() => text)))),
        switchMap((text) =>
          this.api
            .listPeople(text.trim())
            .pipe(catchError(() => of([] as readonly PersonOption[]))),
        ),
        takeUntilDestroyed(inject(DestroyRef)),
      )
      .subscribe((people) => {
        this.people.set(people);
        this.suggestMyself(people);
      });
  }

  ngOnInit(): void {
    // The people are offered as soon as the field is shown; without a search the caller is always among them (P-31).
    this.typedPerson.next('');
  }

  protected allDay(): boolean {
    return this.form().controls.isAllDay.value;
  }

  protected selectedType(): EventType | undefined {
    const id = this.form().controls.typeId.value;
    return this.types().find((type) => type.id === id);
  }

  protected durationText(type: EventType): string {
    return `duración de ${durationLabel(type.defaultDurationMinutes)}`;
  }

  protected reminderText(type: EventType): string {
    return reminderLabel(type.defaultReminderMinutes).toLowerCase();
  }

  /** The presets, plus the current value of an activity that has another one (it must stay selectable). */
  protected reminderOptions(): readonly { minutes: number; label: string }[] {
    const current = this.form().controls.reminderMinutes.value;
    return current === null || REMINDER_PRESETS.some((preset) => preset.minutes === current)
      ? REMINDER_PRESETS
      : [...REMINDER_PRESETS, { minutes: current, label: reminderLabel(current) }];
  }

  /** Cross-field message of the timing (the API checks RN-2 and RN-16 again with the same wording). */
  protected timingError(): string | null {
    const errors = this.form().errors;
    if (errors?.['startTimeRequired']) {
      return 'Indica la hora de inicio.';
    }
    if (errors?.['endDateRequired']) {
      return 'Indica también la fecha de fin.';
    }
    if (errors?.['endBeforeStart']) {
      return 'El fin debe ser posterior al inicio.';
    }
    return null;
  }

  private suggestMyself(people: readonly PersonOption[]): void {
    const responsible = this.form().controls.responsible;
    if (!this.suggestMe() || this.suggested || responsible.value) {
      return;
    }
    const me = people.find((person) => person.isMe);
    if (me) {
      this.suggested = true;
      responsible.setValue(me);
    }
  }
}
