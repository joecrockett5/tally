import { useState, type FormEvent } from 'react';
import { useNavigate } from 'react-router';
import { api } from '../api/client';
import type { Profile } from '../api/types';
import { Avatar } from '../components/Avatar';
import { ErrorNote } from '../components/ErrorNote';
import { Sheet } from '../components/Sheet';
import { useApi } from '../lib/useApi';
import { useProfile } from '../profile/ProfileProvider';

const AVATARS = ['🍎', '🥑', '🥕', '🍋', '🍓', '🥦', '🍌', '🫐', '🍊', '🥝', '🍇', '🌽'];
const COLOURS = ['#1f6f5c', '#2f5d9b', '#9b3f6b', '#b5651d', '#6b4fa0', '#3c7d2b', '#a33a3a', '#2a7f8f'];

export function ProfilePicker() {
  const { choose, profile: active, update, signOut } = useProfile();
  const navigate = useNavigate();
  const { data: profiles, error, loading, reload } = useApi((s) => api.get<Profile[]>('/api/profiles', s), []);
  const [adding, setAdding] = useState(false);
  const [managing, setManaging] = useState(false);
  const [editing, setEditing] = useState<Profile | null>(null);

  const pick = (p: Profile) => {
    if (managing) {
      setEditing(p);
      return;
    }
    choose(p);
    navigate('/diary');
  };

  return (
    <main className="picker">
      <h1 className="picker-title">Who's eating?</h1>
      <ErrorNote error={error} onRetry={reload} />
      {loading && !profiles && <p className="muted center">Loading…</p>}
      <ul className="picker-grid">
        {profiles?.map((p) => (
          <li key={p.id}>
            <button type="button" className={`picker-tile${managing ? ' managing' : ''}`} onClick={() => pick(p)}>
              <Avatar profile={p} size={88} />
              <span className="picker-name">{p.name}</span>
              {managing && <span className="picker-edit">Edit</span>}
            </button>
          </li>
        ))}
        {!managing && (
          <li>
            <button type="button" className="picker-tile picker-add" onClick={() => setAdding(true)}>
              <span className="avatar avatar-add" aria-hidden="true">
                +
              </span>
              <span className="picker-name">Add profile</span>
            </button>
          </li>
        )}
      </ul>
      {profiles && profiles.length > 0 && (
        <button type="button" className="button button-ghost picker-manage" onClick={() => setManaging((m) => !m)}>
          {managing ? 'Done' : 'Manage profiles'}
        </button>
      )}

      {adding && (
        <AddProfileSheet
          onClose={() => setAdding(false)}
          onCreated={(p) => {
            setAdding(false);
            choose(p);
            navigate('/diary');
          }}
        />
      )}
      {editing && (
        <EditProfileSheet
          profile={editing}
          onClose={() => setEditing(null)}
          onSaved={(p) => {
            setEditing(null);
            if (active?.id === p.id) update(p);
            reload();
          }}
          onDeleted={(id) => {
            setEditing(null);
            if (active?.id === id) signOut();
            reload();
          }}
        />
      )}
    </main>
  );
}

function AddProfileSheet({ onClose, onCreated }: { onClose: () => void; onCreated: (p: Profile) => void }) {
  const [name, setName] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<Error>();

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setBusy(true);
    try {
      onCreated(await api.post<Profile>('/api/profiles', { name }));
    } catch (err) {
      setError(err as Error);
      setBusy(false);
    }
  };

  return (
    <Sheet title="Add profile" onClose={onClose}>
      <form onSubmit={submit} className="stack">
        <label className="field">
          <span>Name</span>
          <input autoFocus value={name} maxLength={40} onChange={(e) => setName(e.target.value)} required />
        </label>
        <ErrorNote error={error} />
        <button className="button button-primary" disabled={busy || !name.trim()}>
          Create
        </button>
      </form>
    </Sheet>
  );
}

interface EditProps {
  profile: Profile;
  onClose: () => void;
  onSaved: (p: Profile) => void;
  onDeleted: (id: string) => void;
}

function EditProfileSheet({ profile, onClose, onSaved, onDeleted }: EditProps) {
  const [name, setName] = useState(profile.name);
  const [avatar, setAvatar] = useState(profile.avatar);
  const [colour, setColour] = useState(profile.colour);
  const [confirm, setConfirm] = useState('');
  const [error, setError] = useState<Error>();
  const [busy, setBusy] = useState(false);

  const save = async (e: FormEvent) => {
    e.preventDefault();
    setBusy(true);
    try {
      onSaved(await api.put<Profile>(`/api/profiles/${profile.id}`, { name, avatar, colour }));
    } catch (err) {
      setError(err as Error);
      setBusy(false);
    }
  };

  const remove = async () => {
    setBusy(true);
    try {
      await api.del(`/api/profiles/${profile.id}?confirmName=${encodeURIComponent(confirm)}`);
      onDeleted(profile.id);
    } catch (err) {
      setError(err as Error);
      setBusy(false);
    }
  };

  return (
    <Sheet title={`Edit ${profile.name}`} onClose={onClose}>
      <form onSubmit={save} className="stack">
        <div className="center">
          <Avatar profile={{ name, avatar, colour }} size={72} />
        </div>
        <label className="field">
          <span>Name</span>
          <input value={name} maxLength={40} onChange={(e) => setName(e.target.value)} required />
        </label>
        <fieldset className="field">
          <legend>Avatar</legend>
          <div className="swatches">
            {AVATARS.map((a) => (
              <button
                type="button"
                key={a}
                className={`swatch${a === avatar ? ' selected' : ''}`}
                aria-pressed={a === avatar}
                onClick={() => setAvatar(a)}
              >
                {a}
              </button>
            ))}
          </div>
        </fieldset>
        <fieldset className="field">
          <legend>Colour</legend>
          <div className="swatches">
            {COLOURS.map((c) => (
              <button
                type="button"
                key={c}
                className={`swatch swatch-colour${c === colour ? ' selected' : ''}`}
                style={{ background: c }}
                aria-label={`Colour ${c}`}
                aria-pressed={c === colour}
                onClick={() => setColour(c)}
              />
            ))}
          </div>
        </fieldset>
        <ErrorNote error={error} />
        <button className="button button-primary" disabled={busy || !name.trim()}>
          Save
        </button>
      </form>

      <section className="danger-zone stack">
        <h3>Delete profile</h3>
        <p className="muted">
          This permanently removes {profile.name}'s diary, private foods, recipes, saved meals and goals. Foods they
          shared stay available to everyone.
        </p>
        <label className="field">
          <span>
            Type <strong>{profile.name}</strong> to confirm
          </span>
          <input value={confirm} onChange={(e) => setConfirm(e.target.value)} autoComplete="off" />
        </label>
        <button type="button" className="button button-danger" disabled={busy || confirm !== profile.name} onClick={remove}>
          Delete {profile.name}
        </button>
      </section>
    </Sheet>
  );
}
