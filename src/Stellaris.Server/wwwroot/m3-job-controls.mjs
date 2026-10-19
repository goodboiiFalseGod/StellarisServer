export function appendJobControls(parent, { document, jobId, count, disabled, pending, onSubmit })
{
    const section = document.createElement('section');
    const label = document.createElement('label');
    const input = document.createElement('input');
    const button = document.createElement('button');

    label.textContent = `Вакансии (${jobId})`;
    input.type = 'number';
    input.min = '0';
    input.max = '6';
    input.step = '0.5';
    input.value = String(count);
    input.disabled = Boolean(disabled || pending);
    label.append(input);

    button.type = 'button';
    button.textContent = pending ? 'Ожидание ответа…' : 'Изменить вакансии';
    button.disabled = Boolean(disabled || pending);
    button.addEventListener('click', () =>
    {
        if (!disabled && !pending)
        {
            onSubmit(input.value);
        }
    });

    section.append(label, button);
    parent.append(section);
    return section;
}
