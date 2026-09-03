export function truncate(str: string, length: number): string {
    return str.length > length ? `${str.substring(0, length)}...` : str;
}

export function capitalize(str: string): string {
    return str.charAt(0).toUpperCase() + str.slice(1);
}

export function slugify(str: string): string {
    return str
        .toLowerCase()
        .trim()
        .replaceAll(/[^\w\s-]/g, "")
        .replaceAll(/[\s_-]+/g, "-")
        .replaceAll(/(?:^-+|-+$)/g, "");
}
