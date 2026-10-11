from pathlib import Path
import UnityPy

game = Path(r'C:/Program Files (x86)/Steam/steamapps/common/Quasimorph/Quasimorph_Data')
out = Path(__file__).resolve().parents[1] / 'src' / 'QuasiRobotics' / 'Assets'
names = {'BigCrossButton_Hover', 'BigCrossButton_Normal', 'arrowMouseOver', 'arrowNormal', 'arrowPressed'}
for filename in ['resources.assets', 'sharedassets0.assets', 'sharedassets1.assets']:
    env = UnityPy.load(str(game / filename))
    for obj in env.objects:
        if obj.type.name != 'Sprite':
            continue
        data = obj.read()
        if data.m_Name not in names:
            continue
        picture = data.image
        picture.save(out / (data.m_Name + '.png'))
        print(filename, data.m_Name, picture.size, 'ppu', data.m_PixelsToUnits)
        names.remove(data.m_Name)
    if not names:
        break
if names:
    raise RuntimeError('Missing sprites: ' + repr(names))
