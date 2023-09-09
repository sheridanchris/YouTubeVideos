create table feed (
    name varchar(20) primary key,
    url varchar(500) not null
);

insert into feed (name, url) values ('CNN Top Stories', 'http://rss.cnn.com/rss/cnn_topstories.rss');
insert into feed (name, url) values ('BBC Top Stories', 'http://feeds.bbci.co.uk/news/rss.xml');

create table post (
    id serial primary key,
    feed varchar(20) not null references feed(name),
    title varchar(500) not null,
    url varchar(500) not null,
    publishedAt timestamp not null,
    updatedAt timestamp not null
);

create unique index idxUrl on post(url);